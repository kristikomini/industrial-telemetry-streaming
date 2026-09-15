"""
Producer for Motor-Valley Monitor.
Runs 100+ machines concurrently with asyncio, each publishing sensor readings.

Two independent axes, both chosen by environment variable:
  SOURCE     where each reading comes from   -> "sim" (random walk) | "opcua" (live server)
  TRANSPORT  where each reading is sent       -> "kafka" (topic)     | "http" (processor)
Any combination is valid, e.g. SOURCE=opcua TRANSPORT=http for the no-Docker path fed by
a real OPC UA server.
"""

import asyncio
import logging
import os

from sensor import Sensor, SensorReading

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)

SOURCE = os.getenv("SOURCE", "sim").lower()
TRANSPORT = os.getenv("TRANSPORT", "kafka").lower()

KAFKA_BOOTSTRAP = os.getenv("KAFKA_BOOTSTRAP_SERVERS", "localhost:9093")
SENSOR_TOPIC = os.getenv("SENSOR_TOPIC", "sensor-data")
PROCESSOR_URL = os.getenv("PROCESSOR_URL", "http://localhost:8000")
NUM_MACHINES = int(os.getenv("NUM_MACHINES", "120"))
INTERVAL_SECONDS = float(os.getenv("INTERVAL_SECONDS", "0.5"))


# --- Sources -----------------------------------------------------------------------
# A source answers read(machine_id, index) -> SensorReading. The random-walk simulator
# keeps one Sensor per machine; the OPC UA source reads live values from a server.

class SimSource:
    def __init__(self) -> None:
        self._sensors: dict[str, Sensor] = {}

    async def start(self) -> None:
        logger.info("Data source: built-in random-walk simulator")

    async def read(self, machine_id: str, index: int) -> SensorReading:
        sensor = self._sensors.get(machine_id)
        if sensor is None:
            sensor = Sensor(machine_id)
            self._sensors[machine_id] = sensor
        return sensor.read()

    async def stop(self) -> None:
        pass


async def build_source():
    if SOURCE == "opcua":
        from opcua_source import OpcUaSource

        source = OpcUaSource()
        await source.start()
        return source
    source = SimSource()
    await source.start()
    return source


# --- Per-machine loops -------------------------------------------------------------

async def run_sensor_kafka(producer, source, machine_id: str, index: int) -> None:
    while True:
        try:
            reading = await source.read(machine_id, index)
            await producer.send_and_wait(
                SENSOR_TOPIC,
                value=reading.to_json().encode("utf-8"),
                key=machine_id.encode("utf-8"),
            )
        except Exception as e:
            logger.error("Sensor %s error: %s", machine_id, e)
        await asyncio.sleep(INTERVAL_SECONDS)


async def run_sensor_http(client, source, machine_id: str, index: int) -> None:
    url = f"{PROCESSOR_URL}/ingest"
    while True:
        try:
            reading = await source.read(machine_id, index)
            await client.post(
                url,
                content=reading.to_json(),
                headers={"Content-Type": "application/json"},
            )
        except Exception as e:
            logger.error("Sensor %s error: %s", machine_id, e)
        await asyncio.sleep(INTERVAL_SECONDS)


async def main_kafka(source) -> None:
    from aiokafka import AIOKafkaProducer

    producer = AIOKafkaProducer(bootstrap_servers=KAFKA_BOOTSTRAP)
    await producer.start()
    try:
        logger.info("Starting %d machine sensors on topic %s", NUM_MACHINES, SENSOR_TOPIC)
        tasks = [
            asyncio.create_task(run_sensor_kafka(producer, source, f"MACHINE-{i:04d}", i))
            for i in range(1, NUM_MACHINES + 1)
        ]
        await asyncio.gather(*tasks)
    finally:
        await producer.stop()


async def main_http(source) -> None:
    import httpx

    async with httpx.AsyncClient(timeout=5.0) as client:
        logger.info("Starting %d machine sensors -> %s/ingest", NUM_MACHINES, PROCESSOR_URL)
        tasks = [
            asyncio.create_task(run_sensor_http(client, source, f"MACHINE-{i:04d}", i))
            for i in range(1, NUM_MACHINES + 1)
        ]
        await asyncio.gather(*tasks)


async def main() -> None:
    source = await build_source()
    try:
        if TRANSPORT == "http":
            await main_http(source)
        else:
            await main_kafka(source)
    finally:
        await source.stop()


if __name__ == "__main__":
    asyncio.run(main())
