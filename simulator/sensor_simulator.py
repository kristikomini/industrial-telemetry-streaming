#!/usr/bin/env python3
"""
Standalone sensor simulator for Motor-Valley Monitor.
Simulates sensors and sends data via HTTP or MQTT — same format real hardware would use.
Use this to test the pipeline before connecting actual machines.

Usage:
  # HTTP mode (posts to gateway)
  python sensor_simulator.py --mode http --gateway http://localhost:8080 --machines 10

  # MQTT mode (publishes to broker - use MQTT.fx, Node-RED, etc. to subscribe)
  python sensor_simulator.py --mode mqtt --broker localhost --machines 10
"""

import argparse
import json
import random
import sys
import time
from datetime import datetime
from multiprocessing import Process


class SimpleSensor:
    """Minimal sensor sim — no dependency on producer package."""

    def __init__(self, machine_id: str):
        self.machine_id = machine_id
        self._temp = random.uniform(60, 85)
        self._rpm = random.randint(2000, 8000)

    def read(self):
        self._temp += random.uniform(-2, 2)
        self._temp = max(40, min(110, self._temp))
        self._rpm += random.randint(-100, 100)
        self._rpm = max(0, min(10000, self._rpm))
        status = 2 if self._temp > 95 else (1 if self._temp > 85 else 0)
        return type("R", (), {
            "machine_id": self.machine_id,
            "temperature": round(self._temp, 2),
            "rpm": self._rpm,
            "status_code": status,
        })()


def simulate_sensor_http(machine_id: str, gateway_url: str, interval: float):
    """Simulate one sensor, POST readings to gateway."""
    import requests
    sensor = SimpleSensor(machine_id)
    while True:
        try:
            r = sensor.read()
            data = {
                "machine_id": r.machine_id,
                "temperature": r.temperature,
                "rpm": r.rpm,
                "status_code": r.status_code,
            }
            resp = requests.post(f"{gateway_url.rstrip('/')}/ingest", json=data, timeout=5)
            resp.raise_for_status()
        except Exception as e:
            print(f"[{machine_id}] Error: {e}", file=sys.stderr)
        time.sleep(interval)


def simulate_sensor_mqtt(machine_id: str, broker: str, port: int, topic: str, interval: float):
    """Simulate one sensor, publish to MQTT."""
    import paho.mqtt.client as mqtt
    sensor = SimpleSensor(machine_id)

    def on_connect(c, u, flags, rc):
        if rc != 0:
            print(f"[{machine_id}] MQTT connect failed: {rc}", file=sys.stderr)

    client = mqtt.Client(client_id=machine_id)
    client.on_connect = on_connect
    client.connect(broker, port, 60)
    client.loop_start()

    while True:
        try:
            r = sensor.read()
            payload = {
                "machine_id": r.machine_id,
                "temperature": r.temperature,
                "rpm": r.rpm,
                "status_code": r.status_code,
                "timestamp": datetime.utcnow().isoformat() + "Z",
            }
            t = topic.replace("{machine_id}", machine_id)
            client.publish(t, json.dumps(payload))
        except Exception as e:
            print(f"[{machine_id}] Error: {e}", file=sys.stderr)
        time.sleep(interval)


def main():
    ap = argparse.ArgumentParser(description="Simulate sensors for Motor-Valley Monitor")
    ap.add_argument("--mode", choices=["http", "mqtt"], default="http")
    ap.add_argument("--gateway", default="http://localhost:8080", help="Gateway URL (HTTP mode)")
    ap.add_argument("--broker", default="localhost", help="MQTT broker (MQTT mode)")
    ap.add_argument("--port", type=int, default=1883, help="MQTT port")
    ap.add_argument("--topic", default="sensors/{machine_id}/reading",
                    help="MQTT topic template, {machine_id} is replaced")
    ap.add_argument("--machines", type=int, default=10)
    ap.add_argument("--interval", type=float, default=1.0)
    args = ap.parse_args()

    procs = []
    for i in range(1, args.machines + 1):
        mid = f"MACHINE-{i:04d}"
        if args.mode == "http":
            p = Process(target=simulate_sensor_http, args=(mid, args.gateway, args.interval))
        else:
            p = Process(
                target=simulate_sensor_mqtt,
                args=(mid, args.broker, args.port, args.topic, args.interval),
            )
        p.start()
        procs.append(p)

    print(f"Simulating {args.machines} sensors in {args.mode.upper()} mode. Ctrl+C to stop.")
    try:
        for p in procs:
            p.join()
    except KeyboardInterrupt:
        for p in procs:
            p.terminate()


if __name__ == "__main__":
    main()
