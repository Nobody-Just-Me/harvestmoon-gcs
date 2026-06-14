#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARDUPILOT_DIR="${ARDUPILOT_DIR:-$HOME/ardupilot}"
SIM_VEHICLE="${SIM_VEHICLE:-$ARDUPILOT_DIR/Tools/autotest/sim_vehicle.py}"
PARAM_FILE="$SCRIPT_DIR/quadplane_4p1.parm"
SITL_OUT="${SITL_OUT:-udp:127.0.0.1:14550}"

if [[ ! -f "$SIM_VEHICLE" ]]; then
  echo "sim_vehicle.py not found."
  echo "Set ARDUPILOT_DIR to your ArduPilot checkout, for example:"
  echo "  export ARDUPILOT_DIR=\$HOME/ardupilot"
  exit 1
fi

if [[ ! -f "$PARAM_FILE" ]]; then
  echo "Parameter file not found: $PARAM_FILE"
  exit 1
fi

cd "$ARDUPILOT_DIR"

exec python3 "$SIM_VEHICLE" \
  -v ArduPlane \
  -f quadplane \
  --custom-location=-7.275400,112.794700,10,90 \
  --add-param-file="$PARAM_FILE" \
  --out="$SITL_OUT" \
  "$@"
