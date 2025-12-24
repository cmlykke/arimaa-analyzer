import subprocess
import threading
import queue

# --- Helper: async reader so we can read engine output line-by-line ---
def enqueue_output(stream, q):
    for line in iter(stream.readline, b""):
        q.put(line.decode("utf-8").rstrip("\n"))
    stream.close()

# --- Start the engine ---
# On Windows, use "sharp2015.exe"; full path if not in current dir
engine = subprocess.Popen(
    ["sharp2015.exe", "aei"],  # Launch in AEI mode with arg
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
    stderr=subprocess.STDOUT,
    bufsize=-1,  # Use system default buffering to avoid warnings
)

# Queue to collect output
q = queue.Queue()
t = threading.Thread(target=enqueue_output, args=(engine.stdout, q))
t.daemon = True
t.start()

def send(cmd):
    engine.stdin.write((cmd + "\n").encode("utf-8"))
    engine.stdin.flush()

# --- Initiate AEI mode with protocol start ---
send("aei")  # Send "aei" to trigger handshake

# --- Wait for "aeiok" (engine sends protocol-version, id lines first) ---
while True:
    line = q.get()
    print("ENGINE:", line)
    if line.strip() == "aeiok":
        break

send("isready")

# Wait for "readyok"
while True:
    line = q.get()
    print("ENGINE:", line)
    if line.strip() == "readyok":
        break

send("newgame")

# --- Set initial position using standard AEI format ---
# Board string: Standard initial setup (symmetric: rrrrrrrr h c d m e d c h, etc.)
send("setposition g [rrrrrrrrhcdmedch                                HCDMEDCHRRRRRRRR]")

# Optional: Set time limit to prevent indefinite waits (10 seconds per move)
send("setoption name tcmove value 10")

# Ask engine to compute a move
send("go")

# --- Read until we get a bestmove ---
bestmove = None
while True:
    try:
        line = q.get(timeout=0.1)
    except queue.Empty:
        continue

    print("ENGINE:", line)

    if line.startswith("bestmove"):
        bestmove = line
        break

print("\nFinal engine move:", bestmove)

# Clean shutdown
send("quit")
engine.wait()