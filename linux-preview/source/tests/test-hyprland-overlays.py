#!/usr/bin/env python3
"""Test the published UI on X11 with a simulated Hyprland command interface.
Usage: DISPLAY=:88 python3 tests/test-hyprland-overlays.py artifacts/FooMeter-linux-x64/FooMeter.Linux
No real compositor configuration or clients are modified.
"""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

app = str(Path(sys.argv[1]).resolve())
with tempfile.TemporaryDirectory(prefix="foometer-hypr-test-") as temporary:
    root = Path(temporary)
    helper = root / "hyprctl"
    commands = root / "commands.jsonl"
    helper.write_text('''#!/usr/bin/env python3
import json, os, sys
args=sys.argv[1:]
if args==["clients","-j"]:
    print(json.dumps([{"pid":os.getppid(),"title":title,"address":address,"floating":False} for title,address in [("FooMeter Linux","0xf001"),("FooMeter · Events","0xf002"),("FooMeter · Pets","0xf003")]] + [{"pid":os.getppid()+100000,"title":"FooMeter Linux","address":"0xbad","floating":False}]))
else:
    with open(os.environ["FOOMETER_TEST_COMMANDS"],"a") as file: file.write(json.dumps(args)+"\\n")
    print("ok")
''')
    helper.chmod(0o700)
    result = root / "result.json"
    environment = dict(os.environ, PATH=str(root)+os.pathsep+os.environ["PATH"], HYPRLAND_INSTANCE_SIGNATURE="foometer-test", XDG_RUNTIME_DIR=str(root), FOOMETER_TEST_COMMANDS=str(commands))
    completed = subprocess.run([app,"--smoke-ui",str(result)],env=environment,capture_output=True,text=True,timeout=40)
    assert completed.returncode == 0, completed.stderr
    report = json.loads(result.read_text())
    assert report["Passed"], report
    calls = [json.loads(line) for line in commands.read_text().splitlines()]
    assert not any("0xbad" in str(call) for call in calls), "Foreign client was modified"
    for address in ("0xf001","0xf002","0xf003"):
        assert any(c[:2]==["dispatch","setfloating"] and address in c[-1] for c in calls), address
        assert any(c[:2]==["dispatch","resizewindowpixel"] and address in c[-1] for c in calls), address
    print("PASS: compact UI geometry, visible headers, scoped float/resize dispatches, foreign clients untouched")
    print(json.dumps(report))
