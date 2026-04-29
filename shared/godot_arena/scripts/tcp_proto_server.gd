extends Node
# Length-prefixed JSON over TCP control server.
#
# Wire layout (each direction): [u32 BE length][JSON UTF-8 bytes].
# Request shape:  {"method": "env_reset", "request": {...}}
# Response shape: {"ok": true,  "response": {...}}     (success)
#                 {"ok": false, "error":   "..."}      (failure)
#
# This is the IMPLEMENTATION_PLAN Stage 2 risk-mitigation transport: gRPC
# inside Godot via GDExtension turned out to be more work than the PoC
# justified, so we fall back to "plain TCP + framing" with a JSON payload.
# The proto contract is preserved — JSON field names match proto3 exactly,
# so google.protobuf.json_format on the Python side parses every dict
# straight back into a strongly-typed message.

const READ_CHUNK_TIMEOUT_MS: int = 50

@export var port: int = 7654
var arena: Node                              # set by arena_main.gd

var _server: TCPServer = TCPServer.new()
var _clients: Array[StreamPeerTCP] = []
var _read_buffers: Array[PackedByteArray] = []


func _ready() -> void:
    var err: int = _server.listen(port, "0.0.0.0")
    if err != OK:
        push_error("tcp_proto_server: listen on %d failed: %s" % [port, error_string(err)])
        return
    set_process(true)


func _process(_delta: float) -> void:
    while _server.is_connection_available():
        var peer: StreamPeerTCP = _server.take_connection()
        peer.set_no_delay(true)
        _clients.append(peer)
        _read_buffers.append(PackedByteArray())
        print("[tcp_proto_server] client connected (n=%d)" % _clients.size())

    var i: int = 0
    while i < _clients.size():
        var peer := _clients[i]
        peer.poll()
        var status := peer.get_status()
        if status == StreamPeerTCP.STATUS_NONE or status == StreamPeerTCP.STATUS_ERROR:
            _drop_client(i)
            continue
        if status != StreamPeerTCP.STATUS_CONNECTED:
            i += 1
            continue
        _drain(peer, i)
        i += 1


func _drain(peer: StreamPeerTCP, idx: int) -> void:
    var available := peer.get_available_bytes()
    if available <= 0:
        return
    var chunk := peer.get_data(available)
    if chunk[0] != OK:
        return
    _read_buffers[idx].append_array(chunk[1])

    while true:
        var buf: PackedByteArray = _read_buffers[idx]
        if buf.size() < 4:
            break
        # Big-endian length prefix (matches Python's struct.pack(">I")).
        # PackedByteArray.decode_u32 is little-endian only, so we decode
        # the four bytes by hand.
        var msg_len: int = ((buf[0] << 24) | (buf[1] << 16) | (buf[2] << 8) | buf[3])
        if buf.size() < 4 + msg_len:
            break
        var payload: PackedByteArray = buf.slice(4, 4 + msg_len)
        _read_buffers[idx] = buf.slice(4 + msg_len)
        _dispatch(peer, payload.get_string_from_utf8())


func _dispatch(peer: StreamPeerTCP, json_text: String) -> void:
    var parsed: Variant = JSON.parse_string(json_text)
    if typeof(parsed) != TYPE_DICTIONARY:
        _send(peer, {"ok": false, "error": "request was not a JSON object"})
        return
    var method: String = String(parsed.get("method", ""))
    var request: Dictionary = parsed.get("request", {})
    var response: Variant
    match method:
        "env_reset":
            response = arena.env_reset(request)
        "env_step":
            response = arena.env_step(request)
        "env_push_fire":
            response = arena.env_push_fire(request)
        "env_finish":
            response = arena.env_finish(request)
        _:
            _send(peer, {"ok": false, "error": "unknown method: %s" % method})
            return

    if typeof(response) == TYPE_DICTIONARY and response.has("_error"):
        _send(peer, {"ok": false, "error": response["_error"]})
    else:
        _send(peer, {"ok": true, "response": response})


func _send(peer: StreamPeerTCP, payload: Dictionary) -> void:
    var bytes: PackedByteArray = JSON.stringify(payload).to_utf8_buffer()
    var header: PackedByteArray = PackedByteArray()
    var n: int = bytes.size()
    header.append((n >> 24) & 0xFF)
    header.append((n >> 16) & 0xFF)
    header.append((n >> 8) & 0xFF)
    header.append(n & 0xFF)
    peer.put_data(header)
    peer.put_data(bytes)


func _drop_client(idx: int) -> void:
    _clients.remove_at(idx)
    _read_buffers.remove_at(idx)
    print("[tcp_proto_server] client disconnected (n=%d)" % _clients.size())
