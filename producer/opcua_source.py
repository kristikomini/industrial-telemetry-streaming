"""
OPC UA data source for the producer.

Reads live values from an OPC UA server and turns them into the same SensorReading
shape the random-walk simulator produces, so the rest of the pipeline is unchanged.

Defaults target the free **Prosys OPC UA Simulation Server**, which exposes a small set
of shared signals (Sinusoid, Random, Counter, Sawtooth, ...) under a "Simulation" folder.
Because that server has signals, not 120 machines, each machine is given a fixed,
deterministic offset off the one live waveform — so the fleet spreads across a temperature
band (and some machines breach the 90 C threshold) while every value is still driven by
data actually read from the server over the OPC UA protocol.

To point this at a real PLC instead, either:
  * set OPCUA_TEMPERATURE_NODE / OPCUA_RPM_NODE to explicit NodeIds
    (e.g. OPCUA_TEMPERATURE_NODE="ns=2;s=Line1.Machine7.Temp"), or
  * extend read_reading() to look up a per-machine node from a mapping.
"""

import logging
import os
import random
from datetime import datetime

from asyncua import Client

from sensor import SensorReading, StatusCode

logger = logging.getLogger(__name__)

# --- Connection ---
OPCUA_ENDPOINT = os.getenv("OPCUA_ENDPOINT", "opc.tcp://localhost:53530/OPCUA/SimulationServer")

# Prosys publishes its simulation signals under this namespace URI. Resolving the
# namespace *index* at runtime (rather than hardcoding "ns=3") keeps discovery working
# across server versions where the index differs.
PROSYS_SIM_NS = os.getenv("OPCUA_SIM_NAMESPACE", "http://www.prosysopc.com/OPCUA/SimulationNodes/")
PROSYS_SIM_FOLDER = os.getenv("OPCUA_SIM_FOLDER", "Simulation")
TEMP_SIGNAL = os.getenv("OPCUA_TEMPERATURE_SIGNAL", "Sinusoid")  # browse name under the folder
RPM_SIGNAL = os.getenv("OPCUA_RPM_SIGNAL", "Random")

# Explicit NodeIds override discovery entirely (use these for a real server).
TEMP_NODE_ID = os.getenv("OPCUA_TEMPERATURE_NODE")  # e.g. "ns=2;s=Machine1.Temperature"
RPM_NODE_ID = os.getenv("OPCUA_RPM_NODE")

# --- Scaling: raw signal value -> temperature (deg C) ---
# The live signal swings within [IN_MIN, IN_MAX]; map its centre to TEMP_CENTER and its
# swing to +/- TEMP_SWING, then add a per-machine offset so the fleet spreads out.
IN_MIN = float(os.getenv("OPCUA_IN_MIN", "-2.0"))
IN_MAX = float(os.getenv("OPCUA_IN_MAX", "2.0"))
TEMP_CENTER = float(os.getenv("OPCUA_TEMP_CENTER", "80.0"))
TEMP_SWING = float(os.getenv("OPCUA_TEMP_SWING", "10.0"))


def _normalize(raw: float) -> float:
    """Map a raw signal value into [-1, 1] using its configured input range."""
    span = IN_MAX - IN_MIN
    if span == 0:
        return 0.0
    n = (raw - IN_MIN) / span  # 0..1
    return max(-1.0, min(1.0, n * 2.0 - 1.0))


class OpcUaSource:
    """Shared OPC UA client that all simulated machines read from."""

    def __init__(self) -> None:
        self._client: Client | None = None
        self._temp_node = None
        self._rpm_node = None

    async def start(self) -> None:
        logger.info("Connecting to OPC UA server at %s", OPCUA_ENDPOINT)
        self._client = Client(OPCUA_ENDPOINT)
        await self._client.connect()
        logger.info("Connected to OPC UA server")

        self._temp_node = await self._resolve_node(TEMP_NODE_ID, TEMP_SIGNAL)
        self._rpm_node = await self._resolve_node(RPM_NODE_ID, RPM_SIGNAL)
        logger.info(
            "OPC UA nodes bound: temperature=%s rpm=%s",
            self._temp_node.nodeid.to_string() if self._temp_node else None,
            self._rpm_node.nodeid.to_string() if self._rpm_node else None,
        )

    async def _resolve_node(self, explicit_id: str | None, signal_name: str):
        """Use an explicit NodeId when given, else browse the Prosys Simulation folder."""
        assert self._client is not None
        if explicit_id:
            return self._client.get_node(explicit_id)
        try:
            idx = await self._client.get_namespace_index(PROSYS_SIM_NS)
            folder = await self._client.nodes.objects.get_child([f"{idx}:{PROSYS_SIM_FOLDER}"])
            return await folder.get_child([f"{idx}:{signal_name}"])
        except Exception as e:
            logger.error(
                "Could not auto-discover signal '%s' under folder '%s' (%s). "
                "Run 'python opcua_source.py' to list the server's nodes, then set "
                "OPCUA_TEMPERATURE_NODE / OPCUA_RPM_NODE to explicit NodeIds.",
                signal_name, PROSYS_SIM_FOLDER, e,
            )
            raise

    async def read_reading(self, machine_id: str, index: int) -> SensorReading:
        """Produce one reading for a machine from the current live signal value."""
        assert self._temp_node is not None
        raw_temp = float(await self._temp_node.read_value())
        norm = _normalize(raw_temp)

        # Deterministic per-machine offset spreads the fleet across a band (-6..+18 C).
        offset = ((index * 13) % 25) - 6
        temperature = TEMP_CENTER + norm * TEMP_SWING + offset
        temperature = max(40.0, min(110.0, temperature))

        if self._rpm_node is not None:
            raw_rpm = float(await self._rpm_node.read_value())
            rpm = int(2000 + (_normalize(raw_rpm) + 1.0) / 2.0 * 6000)
        else:
            rpm = random.randint(2000, 8000)
        rpm = max(0, min(10000, rpm))

        if temperature > 95:
            status = StatusCode.CRITICAL
        elif temperature > 85:
            status = StatusCode.WARNING
        else:
            status = StatusCode.NORMAL

        return SensorReading(
            machine_id=machine_id,
            temperature=round(temperature, 2),
            rpm=rpm,
            status_code=int(status),
            timestamp=datetime.utcnow().isoformat() + "Z",
        )

    async def stop(self) -> None:
        if self._client is not None:
            await self._client.disconnect()


async def _browse_and_print() -> None:
    """Diagnostic: connect and list the Simulation folder's nodes and current values.

    Run directly to verify connectivity and see the exact NodeIds your server exposes:
        python opcua_source.py
    """
    client = Client(OPCUA_ENDPOINT)
    await client.connect()
    print(f"Connected to {OPCUA_ENDPOINT}")
    try:
        try:
            idx = await client.get_namespace_index(PROSYS_SIM_NS)
            folder = await client.nodes.objects.get_child([f"{idx}:{PROSYS_SIM_FOLDER}"])
            print(f"\nNodes under '{PROSYS_SIM_FOLDER}' (namespace index {idx}):")
            for child in await folder.get_children():
                name = (await child.read_browse_name()).Name
                try:
                    value = await child.read_value()
                except Exception:
                    value = "<no value>"
                print(f"  {name:<12} NodeId={child.nodeid.to_string():<18} value={value}")
        except Exception as e:
            print(f"\nCould not find the Prosys Simulation folder ({e}).")
            print("Listing everything under Objects instead:")
            for child in await client.nodes.objects.get_children():
                name = (await child.read_browse_name()).Name
                print(f"  {name:<20} NodeId={child.nodeid.to_string()}")
    finally:
        await client.disconnect()


if __name__ == "__main__":
    import asyncio

    logging.basicConfig(level=logging.INFO)
    asyncio.run(_browse_and_print())
