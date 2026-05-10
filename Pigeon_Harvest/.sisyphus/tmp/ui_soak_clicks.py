import os
import time
from Xlib import X, XK, display
from Xlib.ext import xtest


def find_window_by_name(root, needle):
    q = [root]
    while q:
        win = q.pop(0)
        try:
            name = win.get_wm_name()
        except Exception:
            name = None
        if name and needle.lower() in name.lower():
            return win
        try:
            q.extend(win.query_tree().children)
        except Exception:
            pass
    return None


def get_window_origin(win):
    try:
        g = win.get_geometry()
        x = g.x
        y = g.y
    except Exception:
        x = 0
        y = 0

    cur = win
    for _ in range(20):
        try:
            parent = cur.query_tree().parent
        except Exception:
            break
        if parent is None or not hasattr(parent, "id"):
            break
        if parent.id == 0 or parent == cur:
            break
        try:
            pg = parent.get_geometry()
            x += pg.x
            y += pg.y
        except Exception:
            pass
        cur = parent

    return x, y


def click_abs(d, x, y):
    xtest.fake_input(d, X.MotionNotify, x=int(x), y=int(y))
    xtest.fake_input(d, X.ButtonPress, 1)
    xtest.fake_input(d, X.ButtonRelease, 1)
    d.sync()


def send_key(d, key_name):
    ks = XK.string_to_keysym(key_name)
    kc = d.keysym_to_keycode(ks)
    xtest.fake_input(d, X.KeyPress, kc)
    xtest.fake_input(d, X.KeyRelease, kc)
    d.sync()


def focus_window(d, win):
    try:
        win.set_input_focus(X.RevertToParent, X.CurrentTime)
    except Exception:
        pass
    d.sync()


def click_rel(d, win, rel_x, rel_y):
    wx, wy = get_window_origin(win)
    click_abs(d, wx + rel_x, wy + rel_y)


def main():
    d = display.Display(os.environ.get("DISPLAY", ":0"))
    root = d.screen().root

    time.sleep(8)
    win = find_window_by_name(root, "Pigeon GCS")
    if win is None:
        print("SOAK_ABORT window_not_found")
        return 2

    focus_window(d, win)
    time.sleep(0.5)

    CONNECT = (110, 34)
    NAV = {
        "flight": (120, 230),
        "map": (120, 334),
        "lora": (120, 490),
        "calibrate": (120, 542),
        "tlog": (120, 594),
    }

    soak_seconds = int(os.environ.get("SOAK_SECONDS", "360"))
    start = time.time()
    rounds = 0
    while time.time() - start < soak_seconds:
        rounds += 1
        focus_window(d, win)

        click_rel(d, win, *CONNECT)
        time.sleep(0.35)
        send_key(d, "Tab")
        send_key(d, "Tab")
        send_key(d, "Down")
        send_key(d, "Up")
        send_key(d, "Escape")
        time.sleep(0.2)

        click_rel(d, win, *NAV["map"])
        time.sleep(0.22)
        click_rel(d, win, *NAV["tlog"])
        time.sleep(0.22)
        click_rel(d, win, *NAV["flight"])
        time.sleep(0.2)
        click_rel(d, win, *NAV["lora"])
        time.sleep(0.2)

        for _ in range(3):
            click_rel(d, win, *NAV["map"])
            time.sleep(0.14)
            click_rel(d, win, *NAV["tlog"])
            time.sleep(0.14)

        click_rel(d, win, *NAV["calibrate"])
        time.sleep(0.2)
        click_rel(d, win, *NAV["flight"])
        time.sleep(0.18)

    print(f"SOAK_CLICKS_DONE rounds={rounds} duration_sec={int(time.time() - start)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
