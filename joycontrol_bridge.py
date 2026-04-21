#!/usr/bin/env python3
"""
joycontrol_bridge.py - Minimal joycontrol wrapper for TomodachiDraw.

Runs as a subprocess. Reads commands from stdin, writes status to stdout.
Commands (one per line):
  btn <button>   - press button (e.g. btn a, btn d_up, btn -)
  quit           - exit cleanly

Status output:
  STATUS:ADVERTISING  - waiting for Switch
  STATUS:CONNECTED    - Switch connected
  STATUS:ERROR:<msg>  - something went wrong
"""

import asyncio
import sys
import os
import logging

# Add joycontrol source to path
sys.path.insert(0, '/home/pi/joycontrol')

from joycontrol.server import create_hid_server
from joycontrol.controller import Controller
from joycontrol.memory import FlashMemory
from joycontrol.controller_state import ControllerState, button_push, button_release
from joycontrol.protocol import controller_protocol_factory

logging.basicConfig(level=logging.WARNING)

# Switch BT address — set after first pairing for faster reconnect
SWITCH_BT_ADDRESS = 'E0:EF:BF:36:43:99'

async def main():
    reconnect = '--reconnect' in sys.argv

    # FlashMemory() with no args creates default calibration data — required for stick emulation
    spi_flash = FlashMemory()
    factory = controller_protocol_factory(Controller.PRO_CONTROLLER, spi_flash=spi_flash)

    print('STATUS:ADVERTISING', flush=True)

    try:
        transport, protocol = await create_hid_server(
            factory,
            reconnect_bt_addr=SWITCH_BT_ADDRESS if reconnect else None
        )
    except Exception as e:
        print(f'STATUS:ERROR:{e}', flush=True)
        return

    controller_state = protocol.get_controller_state()
    await controller_state.connect()

    print('STATUS:CONNECTED', flush=True)

    loop = asyncio.get_event_loop()

    # Read commands from stdin in a background thread
    def read_stdin():
        for line in sys.stdin:
            line = line.strip()
            if not line:
                continue
            asyncio.run_coroutine_threadsafe(handle_command(line, controller_state), loop)

    import threading
    t = threading.Thread(target=read_stdin, daemon=True)
    t.start()

    # Keep running until stdin closes
    await loop.run_in_executor(None, t.join)


async def handle_command(cmd, controller_state: ControllerState):
    parts = cmd.split()
    if not parts:
        return

    if parts[0] == 'quit':
        asyncio.get_event_loop().stop()
        return

    if parts[0] == 'stick' and len(parts) >= 3:
        # stick l up/down/left/right/center
        # up/down sets vertical axis; left/right sets horizontal axis
        # Both can be active simultaneously for diagonal movement
        which = parts[1]  # l or r
        direction = parts[2]
        stick = controller_state.l_stick_state if which == 'l' else controller_state.r_stick_state
        if stick is not None:
            if direction == 'up':
                # Only set vertical, preserve horizontal
                stick.set_v(stick._calibration.v_center + stick._calibration.v_max_above_center)
            elif direction == 'down':
                stick.set_v(stick._calibration.v_center - stick._calibration.v_max_below_center)
            elif direction == 'left':
                # Only set horizontal, preserve vertical
                stick.set_h(stick._calibration.h_center - stick._calibration.h_max_below_center)
            elif direction == 'right':
                stick.set_h(stick._calibration.h_center + stick._calibration.h_max_above_center)
            elif direction == 'center':
                stick.set_center()
            elif direction == 'upleft':
                stick.set_v(stick._calibration.v_center + stick._calibration.v_max_above_center)
                stick.set_h(stick._calibration.h_center - stick._calibration.h_max_below_center)
        await controller_state.send()
        return

    if parts[0] == 'btn' and len(parts) >= 2:
        button_str = parts[1]

        if button_str == '-':
            # Release all buttons — reset each known button explicitly
            for b in controller_state.button_state.get_available_buttons():
                try:
                    controller_state.button_state.set_button(b, pushed=False)
                except:
                    pass
        else:
            # Support multiple simultaneous buttons with + separator (e.g. l+r)
            buttons = button_str.split('+')
            for b in buttons:
                b = b.strip()
                if b:
                    try:
                        controller_state.button_state.set_button(b, pushed=True)
                    except Exception as e:
                        print(f'STATUS:ERROR:bad button {b}: {e}', flush=True)

        await controller_state.send()


if __name__ == '__main__':
    asyncio.run(main())
