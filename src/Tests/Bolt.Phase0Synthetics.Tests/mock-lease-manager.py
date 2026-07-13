#!/usr/bin/env python3
import argparse
import os
import sys


def positive_decimal(value: str) -> int:
    if not value.isascii() or not value.isdecimal() or int(value) <= 0:
        raise argparse.ArgumentTypeError("expected a positive decimal")
    return int(value)


arguments = sys.argv[1:]
separator = arguments.index("--") if "--" in arguments else len(arguments)
control_arguments = arguments[:separator]
supervised_command = arguments[separator + 1 :] if separator < len(arguments) else []

parser = argparse.ArgumentParser()
parser.add_argument("--project-name", required=True)
parser.add_argument("--deployment-uid", required=True, type=positive_decimal)
subparsers = parser.add_subparsers(dest="command", required=True)

heartbeat = subparsers.add_parser("heartbeat")
heartbeat.add_argument("--run-id", required=True, type=positive_decimal)
heartbeat.add_argument("--run-attempt", required=True, type=positive_decimal)
heartbeat.add_argument("--phase", required=True)
heartbeat.add_argument("--mutation-began", required=True, action="store_true")

supervise = subparsers.add_parser("supervise")
supervise.add_argument("--run-id", required=True, type=positive_decimal)
supervise.add_argument("--run-attempt", required=True, type=positive_decimal)
supervise.add_argument("--phase", required=True)
supervise.add_argument("--mutation-began", required=True, action="store_true")
supervise.add_argument("--timeout-seconds", required=True, type=positive_decimal)
supervise.add_argument("--quiet", required=True, action="store_true")

parsed = parser.parse_args(control_arguments)
expected_uid = os.getuid()
if parsed.project_name != "phase0-harness" or parsed.deployment_uid != expected_uid:
    parser.error("unexpected harness project or deployment identity")

if parsed.command == "heartbeat":
    if supervised_command or parsed.phase != "synthetic-canary":
        parser.error("invalid heartbeat contract")
    raise SystemExit(0)

if parsed.phase != "redis-canary" or parsed.timeout_seconds != 360 or not supervised_command:
    parser.error("invalid supervise contract")
os.execvp(supervised_command[0], supervised_command)
