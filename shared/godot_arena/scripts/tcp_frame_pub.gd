extends Node
# Frame publisher. Same wire layout as shared/zmq_frame_pub: each frame
# is a 16-byte little-endian header (frame_id u64, stamp_ns u64) followed
# by raw RGB888 pixels. The transport is plain TCP rather than ZMQ
# because Godot does not ship a ZMQ binding and we deferred the
# GDExtension build to post-Stage-2.
#
# A subscriber reads frames as they're broadcast; clients connecting
# mid-episode start at the next frame, no replay buffer.

const HEADER_BYTES: int = 16
const TARGET_FPS: int = 60

@export var port: int = 7655

var _server: TCPServer = TCPServer.new()
var _clients: Array[StreamPeerTCP] = []
var _accumulator: float = 0.0
var _frame_id: int = 0
var _last_frame_capture: PackedByteArray = PackedByteArray()


func _ready() -> void:
    var err: int = _server.listen(port, "0.0.0.0")
    if err != OK:
        push_error("tcp_frame_pub: listen on %d failed: %s" % [port, error_string(err)])
        return


func _process(delta: float) -> void:
    while _server.is_connection_available():
        var peer: StreamPeerTCP = _server.take_connection()
        peer.set_no_delay(true)
        _clients.append(peer)

    if _clients.is_empty():
        return

    _accumulator += delta
    var period: float = 1.0 / float(TARGET_FPS)
    if _accumulator < period:
        return
    _accumulator = 0.0

    var captured: PackedByteArray = _capture_viewport_rgb888()
    if captured.is_empty():
        return

    _frame_id += 1
    var stamp_ns: int = Time.get_ticks_usec() * 1000
    var header: PackedByteArray = _make_header(_frame_id, stamp_ns)

    var i: int = 0
    while i < _clients.size():
        var peer := _clients[i]
        peer.poll()
        if peer.get_status() != StreamPeerTCP.STATUS_CONNECTED:
            _clients.remove_at(i)
            continue
        peer.put_data(header)
        peer.put_data(captured)
        i += 1


func _make_header(frame_id: int, stamp_ns: int) -> PackedByteArray:
    var header: PackedByteArray = PackedByteArray()
    header.resize(HEADER_BYTES)
    header.encode_u64(0, frame_id)
    header.encode_u64(8, stamp_ns)
    return header


func _capture_viewport_rgb888() -> PackedByteArray:
    var viewport: Viewport = get_viewport()
    if viewport == null:
        return PackedByteArray()
    var image: Image = viewport.get_texture().get_image()
    if image == null:
        return PackedByteArray()
    if image.get_format() != Image.FORMAT_RGB8:
        image.convert(Image.FORMAT_RGB8)
    return image.get_data()
