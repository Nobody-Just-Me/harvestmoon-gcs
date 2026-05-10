import os
import time
import sys
from Xlib import X, XK, display
from Xlib.ext import xtest


def keysym(name):
    return XK.string_to_keysym(name)


def send_key(d, key_name):
    ks = keysym(key_name)
    if ks == 0:
        raise RuntimeError(f"Unknown keysym: {key_name}")
    kc = d.keysym_to_keycode(ks)
    xtest.fake_input(d, X.KeyPress, kc)
    xtest.fake_input(d, X.KeyRelease, kc)
    d.sync()


def send_text(d, text):
    for ch in text:
        if ch == '\n':
            send_key(d, 'Return')
            continue
        ks = XK.string_to_keysym(ch)
        if ks == 0:
            continue
        kc = d.keysym_to_keycode(ks)
        need_shift = ch.isupper() or ch in r'~!@#$%^&*()_+{}|:"<>?'
        if need_shift:
            shift = d.keysym_to_keycode(XK.string_to_keysym('Shift_L'))
            xtest.fake_input(d, X.KeyPress, shift)
            xtest.fake_input(d, X.KeyPress, kc)
            xtest.fake_input(d, X.KeyRelease, kc)
            xtest.fake_input(d, X.KeyRelease, shift)
        else:
            xtest.fake_input(d, X.KeyPress, kc)
            xtest.fake_input(d, X.KeyRelease, kc)
        d.sync()
        time.sleep(0.02)


def focus_root(d):
    root = d.screen().root
    root.set_input_focus(X.RevertToPointerRoot, X.CurrentTime)
    d.sync()


def alt_combo(d, key):
    alt = d.keysym_to_keycode(XK.string_to_keysym('Alt_L'))
    kc = d.keysym_to_keycode(XK.string_to_keysym(key))
    xtest.fake_input(d, X.KeyPress, alt)
    xtest.fake_input(d, X.KeyPress, kc)
    xtest.fake_input(d, X.KeyRelease, kc)
    xtest.fake_input(d, X.KeyRelease, alt)
    d.sync()


def main():
    disp_name = os.environ.get('DISPLAY', ':0')
    d = display.Display(disp_name)

    # Give app time to fully paint
    time.sleep(6)
    focus_root(d)

    # Warmup open connect dialog and close
    alt_combo(d, 'c')
    time.sleep(0.8)
    send_key(d, 'Escape')
    time.sleep(0.5)

    # 10 minute soak loop
    start = time.time()
    rounds = 0

    # Sidebar key travel assumptions via Tab/Enter navigation
    while time.time() - start < 600:
        rounds += 1

        # Open connect dialog repeatedly to stress topbar interaction
        alt_combo(d, 'c')
        time.sleep(0.45)
        send_key(d, 'Tab')
        send_key(d, 'Tab')
        send_key(d, 'Down')
        send_key(d, 'Up')
        send_key(d, 'Escape')
        time.sleep(0.35)

        # Move focus-heavy controls (dropdown stress)
        for _ in range(8):
            send_key(d, 'Tab')
            time.sleep(0.04)
        send_key(d, 'Down')
        send_key(d, 'Down')
        send_key(d, 'Up')
        time.sleep(0.25)

        # Simulate menu switching pressure via Alt shortcuts fallback
        for key in ['1', '2', '3', '4', '5', '6', '7', '8']:
            alt_combo(d, key)
            time.sleep(0.22)

        # Additional tab traversal + enter (dropdown/button activation)
        for _ in range(10):
            send_key(d, 'Tab')
            time.sleep(0.03)
        send_key(d, 'Return')
        time.sleep(0.2)
        send_key(d, 'Escape')
        time.sleep(0.2)

    print(f"SOAK_DONE rounds={rounds} duration_sec={int(time.time()-start)}")


if __name__ == '__main__':
    main()
