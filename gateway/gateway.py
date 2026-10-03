"""
Sensor Gateway for Motor-Valley Monitor.
Ingests sensor data via HTTP or MQTT and forwards to Kafka.
Use this with simulators or real hardware — same interface for both.
"""

import asyncio
import json
import logging
import os
from contextlib import asynccontextmanager

from aiokafka import AIOKafkaProducer
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import paho.mqtt.client as mqtt

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)

KAFKA_BOOTSTRAP = os.getenv("KAFKA_BOOTSTRAP_SERVERS", "localhost:9093")
SENSOR_TOPIC = os.getenv("SENSOR_TOPIC", "sensor-data")
MQTT_BROKER = os.getenv("MQTT_BROKER", "localhost")
MQTT_PORT = int(os.getenv("MQTT_PORT", "1883"))
MQTT_TOPIC = os.getenv("MQTT_TOPIC", "sensors/+/reading")

# Shared Kafka producer
kafka_producer: AIOKafkaProducer | None = None


class SensorReading(BaseModel):
    machine_id: str
    temperature: float
    rpm: int
    status_code: int = 0


def make_payload(reading: SensorReading) -> dict:
    import datetime
    return {
        "machine_id": reading.machine_id,
        "temperature": reading.temperature,
        "rpm": reading.rpm,
        "status_code": reading.status_code,
        "timestamp": datetime.datetime.utcnow().isoformat() + "Z",
    }


async def forward_to_kafka(payload: dict) -> None:
    global kafka_producer
    if not kafka_producer:
        raise RuntimeError("Kafka not ready")
    mid = payload["machine_id"]
    await kafka_producer.send_and_wait(
        SENSOR_TOPIC,
        value=json.dumps(payload).encode("utf-8"),
        key=mid.encode("utf-8"),
    )
    logger.debug("Forwarded %s: T=%.1f°C", mid, payload["temperature"])


def on_mqtt_connect(client, userdata, flags, rc):
    if rc == 0:
        logger.info("MQTT connected, subscribing to %s", MQTT_TOPIC)
        client.subscribe(MQTT_TOPIC)
    else:
        logger.error("MQTT connection failed: %s", rc)


_loop: asyncio.AbstractEventLoop | None = None


def on_mqtt_message(client, userdata, msg):
    global _loop
    try:
        payload = json.loads(msg.payload.decode())
        r = SensorReading(
            machine_id=payload.get("machine_id", "UNKNOWN"),
            temperature=float(payload.get("temperature", 0)),
            rpm=int(payload.get("rpm", 0)),
            status_code=int(payload.get("status_code", 0)),
        )
        full = make_payload(r)
        if _loop:
            asyncio.run_coroutine_threadsafe(forward_to_kafka(full), _loop)
    except Exception as e:
        logger.error("MQTT message error: %s", e)


@asynccontextmanager
async def lifespan(app: FastAPI):
    global kafka_producer, _loop
    _loop = asyncio.get_event_loop()
    kafka_producer = AIOKafkaProducer(bootstrap_servers=KAFKA_BOOTSTRAP)
    await kafka_producer.start()
    logger.info("Kafka producer started")

    # Start MQTT client in background
    mqtt_client = mqtt.Client()
    mqtt_client.on_connect = on_mqtt_connect
    mqtt_client.on_message = on_mqtt_message
    try:
        mqtt_client.connect(MQTT_BROKER, MQTT_PORT, 60)
        mqtt_client.loop_start()
    except Exception as e:
        logger.warning("MQTT broker not available: %s (HTTP ingestion still works)", e)

    yield
    mqtt_client.loop_stop()
    mqtt_client.disconnect()
    await kafka_producer.stop()
    kafka_producer = None


app = FastAPI(title="Motor-Valley Sensor Gateway", lifespan=lifespan)


@app.get("/")
def root():
    """Root route - avoid 404 when visiting gateway in browser."""
    return {
        "service": "Motor-Valley Sensor Gateway",
        "endpoints": {
            "POST /ingest": "Submit sensor readings (machine_id, temperature, rpm, status_code)",
            "GET /health": "Health check",
            "GET /docs": "OpenAPI documentation",
        },
    }


@app.post("/ingest")
async def ingest_reading(reading: SensorReading):
    """Accept a sensor reading and forward to Kafka. Use this for simulators or real hardware."""
    payload = make_payload(reading)
    await forward_to_kafka(payload)
    return {"ok": True, "machine_id": reading.machine_id}


@app.get("/health")
def health():
    return {"status": "ok", "kafka": kafka_producer is not None}
