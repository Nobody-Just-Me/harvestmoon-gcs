import os
from Xlib import display

d = display.Display(os.environ.get('DISPLAY', ':0'))
root = d.screen().root

def walk(win, depth=0):
    try:
        name = win.get_wm_name()
    except Exception:
        name = None
    if name:
        print('  '*depth + f"id={win.id} name={name}")
    try:
        children = win.query_tree().children
    except Exception:
        children = []
    for c in children:
        walk(c, depth+1)

walk(root)
