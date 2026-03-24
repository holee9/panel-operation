from dataclasses import dataclass, field
from pathlib import Path

from cocotb.triggers import RisingEdge

from .conftest import VECTOR_ROOT, reset_sync, start_clock


@dataclass
class VectorStep:
    cycle: int
    inputs: dict = field(default_factory=dict)
    outputs: dict = field(default_factory=dict)


@dataclass
class VectorFile:
    module_name: str = ""
    spec_name: str = ""
    clock_name: str = "clk"
    signal_inputs: list[str] = field(default_factory=list)
    signal_outputs: list[str] = field(default_factory=list)
    vectors: list[VectorStep] = field(default_factory=list)


def _parse_value(token: str):
    if token.startswith("[") and token.endswith("]"):
        body = token[1:-1]
        if not body:
            return []
        return [int(item, 16) for item in body.split(",")]
    return int(token, 16)


def load_hex_vectors(path: str | Path) -> VectorFile:
    vector_file = VectorFile()
    path = Path(path)
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        if line.startswith("@MODULE "):
            vector_file.module_name = line[8:]
            continue
        if line.startswith("@SPEC "):
            vector_file.spec_name = line[6:]
            continue
        if line.startswith("@CLOCK "):
            vector_file.clock_name = line[7:]
            continue
        if line.startswith("@SIGNALS_IN"):
            vector_file.signal_inputs = line.split()[1:]
            continue
        if line.startswith("@SIGNALS_OUT"):
            vector_file.signal_outputs = line.split()[1:]
            continue

        tokens = line.split()
        cycle = int(tokens[0], 16)
        index = 1
        inputs = {}
        outputs = {}
        for signal in vector_file.signal_inputs:
            inputs[signal] = _parse_value(tokens[index])
            index += 1
        for signal in vector_file.signal_outputs:
            outputs[signal] = _parse_value(tokens[index])
            index += 1
        vector_file.vectors.append(VectorStep(cycle=cycle, inputs=inputs, outputs=outputs))
    return vector_file


def resolve_vector_path(filename: str) -> Path:
    direct_path = VECTOR_ROOT / filename
    if direct_path.exists():
        return direct_path

    for candidate in sorted(VECTOR_ROOT.glob("spec*/")):
        nested_path = candidate / filename
        if nested_path.exists():
            return nested_path

    return direct_path


def _resolve_signal_name(signal_name: str, aliases: dict[str, str] | None):
    if aliases and signal_name in aliases:
        return aliases[signal_name]
    return signal_name


def _set_signal(dut, signal_name: str, value, aliases: dict[str, str] | None = None):
    if isinstance(value, list):
        return
    actual_name = _resolve_signal_name(signal_name, aliases)
    if hasattr(dut, actual_name):
        getattr(dut, actual_name).value = value


def _assert_signal(dut, signal_name: str, expected, aliases: dict[str, str] | None = None):
    if isinstance(expected, list):
        return
    actual_name = _resolve_signal_name(signal_name, aliases)
    assert hasattr(dut, actual_name), f"missing DUT signal: {actual_name}"
    actual = int(getattr(dut, actual_name).value)
    assert actual == expected, f"{actual_name}: expected 0x{expected:X}, got 0x{actual:X}"


async def run_vector_test(
    dut,
    vector_filename: str,
    *,
    clock_name: str = "clk",
    reset_name: str = "rst_n",
    aliases: dict[str, str] | None = None,
    init_values: dict[str, int] | None = None,
):
    vector_file = load_hex_vectors(resolve_vector_path(vector_filename))
    clk = getattr(dut, clock_name)
    await start_clock(clk)
    if init_values:
        for signal_name, value in init_values.items():
            _set_signal(dut, signal_name, value, aliases)
    await reset_sync(dut, clk_name=clock_name, rst_name=reset_name)
    for step in vector_file.vectors:
        for signal_name, value in step.inputs.items():
            _set_signal(dut, signal_name, value, aliases)
        await RisingEdge(clk)
        for signal_name, value in step.outputs.items():
            _assert_signal(dut, signal_name, value, aliases)
