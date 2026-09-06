import pathlib, subprocess, sys, uuid, queue, threading
root=pathlib.Path(sys.argv[1]).resolve()
pipe='yuzuha-aot-test-'+uuid.uuid4().hex
server_dir=sys.argv[2] if len(sys.argv)>2 else 'test-server'
server=subprocess.Popen([str(root/server_dir/'Server.exe'),pipe],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
q=queue.Queue()
threading.Thread(target=lambda:q.put(server.stdout.readline()),daemon=True).start()
try:
    ready=q.get(timeout=15).strip()
    if ready!='READY': raise RuntimeError(server.stderr.read())
    subprocess.run([str(root/'test-client/Client.exe'),pipe],check=True,timeout=20)
finally:
    if server.poll() is None:
        try:server.stdin.write('\n');server.stdin.flush()
        except OSError:pass
    try:server.wait(timeout=10)
    except subprocess.TimeoutExpired:server.kill();server.wait()
