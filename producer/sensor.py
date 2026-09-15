"""
Sensor simulation for Motor-Valley Monitor.
Generates realistic machine data: Machine_ID, Temperature, RPM, Status_Code.

Temperature is modelled as a mean-reverting (Ornstein-Uhlenbeck-style) process: each
machine sits near its own resting temperature and small noise pulls it back, so a healthy
floor stays quiet. Occasionally a machine develops a *heating fault* — its target climbs
past the critical threshold for a sustained stretch, producing a real run of consecutive
breaches (which is what the processor's 3-in-a-row rule is meant to catch) — then recovers.
This keeps alerts rare, clustered per machine, and meaningful rather than a constant flood.
Intensity is tunable via the FAULT_* / REVERSION / NOISE environment variables below.
"""

import json
import os
import random
from dataclasses import dataclass, asdict
from datetime import datetime
from enum import IntEnum

# How hard temperature is pulled back toward its target each reading (0..1).
REVERSION = float(os.getenv("SENSOR_REVERSION", "0.15"))
# Std-dev of the per-reading gaussian jitter, in degrees C.
NOISE = float(os.getenv("SENSOR_NOISE", "1.0"))
# Per-reading probability that a healthy machine develops a heating fault.
FAULT_START_PROB = float(os.getenv("SENSOR_FAULT_PROB", "0.0006"))
# Target temperature a faulting machine climbs toward (well above the 90 C threshold).
FAULT_TARGET = float(os.getenv("SENSOR_FAULT_TARGET", "100.0"))
# A fault lasts a random number of readings in this inclusive range.
FAULT_MIN_TICKS = int(os.getenv("SENSOR_FAULT_MIN_TICKS", "6"))
FAULT_MAX_TICKS = int(os.getenv("SENSOR_FAULT_MAX_TICKS", "14"))


class StatusCode(IntEnum):
    """Machine status codes."""

    NORMAL = 0
    WARNING = 1
    CRITICAL = 2
    OFFLINE = 3
    MAINTENANCE = 4


@dataclass
class SensorReading:
    """A single sensor reading from a machine."""

    machine_id: str
    temperature: float
    rpm: int
    status_code: int
    timestamp: str

    def to_json(self) -> str:
        return json.dumps(asdict(self))


class Sensor:
    """Simulates a single machine sensor with realistic, evolving data."""

    def __init__(self, machine_id: str) -> None:
        self.machine_id = machine_id
        # Each machine has its own healthy resting temperature (~68-78 C).
        self._baseline = random.uniform(68.0, 78.0)
        self._temperature = self._baseline + random.uniform(-3.0, 3.0)
        self._rpm = random.randint(2000, 8000)
        self._status = StatusCode.NORMAL
        self._fault_ticks = 0  # >0 while this machine is in a heating fault

    def _evolve(self) -> None:
        """Advance one reading: mean-revert toward the current target, with occasional
        sustained heating faults."""
        if self._fault_ticks > 0:
            # In a fault: aim well above the threshold so breaches are consecutive.
            self._fault_ticks -= 1
            target = FAULT_TARGET
        else:
            target = self._baseline
            if random.random() < FAULT_START_PROB:
                self._fault_ticks = random.randint(FAULT_MIN_TICKS, FAULT_MAX_TICKS)

        # Mean-reverting step: pulled toward target, plus small gaussian noise.
        self._temperature += REVERSION * (target - self._temperature) + random.gauss(0.0, NOISE)
        self._temperature = max(40.0, min(110.0, self._temperature))

        self._rpm += random.randint(-100, 100)
        self._rpm = max(0, min(10000, self._rpm))

        if self._temperature > 95:
            self._status = StatusCode.CRITICAL
        elif self._temperature > 85:
            self._status = StatusCode.WARNING
        elif random.random() < 0.001:
            self._status = StatusCode.MAINTENANCE
        else:
            self._status = StatusCode.NORMAL

    def read(self) -> SensorReading:
        """Produce one reading."""
        self._evolve()
        return SensorReading(
            machine_id=self.machine_id,
            temperature=round(self._temperature, 2),
            rpm=self._rpm,
            status_code=int(self._status),
            timestamp=datetime.utcnow().isoformat() + "Z",
        )
