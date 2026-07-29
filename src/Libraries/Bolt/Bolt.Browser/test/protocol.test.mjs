import assert from 'node:assert/strict';
import test from 'node:test';

import {
    HEADER_SIZE,
    MAX_ACTOR_TOKEN_SIZE,
    MAX_FRAME_SIZE,
    MAX_STRING_SIZE,
    MAX_TOPIC_SIZE,
    MAX_BATCH_BYTES,
    MAX_BATCH_FRAMES,
    WIRE_VERSION,
    bytesToGuid,
    guidToBytes,
    readBatch,
    readFrameType,
    readRegisterAck,
    readRegisterAckDetails,
    readRequest,
    readRequestCancel,
    writeAck,
    writeBatch,
    writePublish,
    writeRegister,
    writeRequest,
    writeRequestCancel,
    writeSubscribe,
} from '../dist/protocol.js';

const guid = '00112233-4455-6677-8899-aabbccddeeff';

test('UUID conversion is strict and round-trips the .NET byte layout', () => {
    const bytes = guidToBytes(guid);

    assert.equal(bytesToGuid(bytes), guid);
    assert.throws(() => guidToBytes('00112233-4455-6677-8899-aabbccddeezz'), /Invalid UUID/);
    assert.throws(() => guidToBytes('00112233445566778899aabbccddeeff'), /Invalid UUID/);
    assert.throws(() => bytesToGuid(new Uint8Array(15)), /requires 16 bytes/);
});

test('decoders return payload views instead of copies', () => {
    const frame = writeRequest(guid, 1, 2, 3, new Uint8Array([4, 5, 6]));
    const decoded = readRequest(frame);

    assert.ok(decoded);
    assert.equal(decoded.payload.buffer, frame.buffer);
    frame[HEADER_SIZE.Request] = 9;
    assert.equal(decoded.payload[0], 9);
});

test('writers and readers enforce the 8 MiB frame ceiling', () => {
    const maximumPayload = new Uint8Array(MAX_FRAME_SIZE - HEADER_SIZE.Request);
    assert.equal(writeRequest(guid, 0, 0, 0, maximumPayload).length, MAX_FRAME_SIZE);

    assert.throws(
        () => writeRequest(guid, 0, 0, 0, new Uint8Array(maximumPayload.length + 1)),
        /Bolt frames must be/,
    );
    assert.equal(readRequest(new Uint8Array(MAX_FRAME_SIZE + 1)), null);
    assert.throws(() => readFrameType(new Uint8Array()), /Bolt frames must be/);
});

test('64-bit number inputs must be safe integers', () => {
    assert.throws(
        () => writeAck('events', 'browser', Number.MAX_SAFE_INTEGER + 1),
        /safe integers/,
    );
    assert.doesNotThrow(() => writeAck('events', 'browser', Number.MAX_SAFE_INTEGER));
    assert.throws(() => writeAck('events', 'browser', 1n << 63n), /signed 64-bit/);
});

test('request cancellation round-trips its request id', () => {
    const frame = writeRequestCancel(guid);

    assert.equal(frame.length, HEADER_SIZE.RequestCancel);
    assert.equal(readRequestCancel(frame), guid);
    assert.equal(readRequestCancel(frame.subarray(0, frame.length - 1)), null);
});

test('string writers enforce the .NET UTF-8 byte limits', () => {
    assert.doesNotThrow(() => writeRegister('a'.repeat(MAX_STRING_SIZE), 'browser'));
    assert.throws(() => writeRegister('', 'browser'), /clientId/);
    assert.throws(() => writeRegister('a'.repeat(MAX_STRING_SIZE + 1), 'browser'), /clientId/);
    assert.throws(() => writePublish('', false, new Uint8Array()), /topic/);
    assert.throws(() => writePublish('a'.repeat(MAX_TOPIC_SIZE + 1), false, new Uint8Array()), /topic/);
    assert.throws(
        () => writeSubscribe('events', '', false, 'a'.repeat(MAX_ACTOR_TOKEN_SIZE + 1)),
        /actorAccessToken/,
    );
});

test('registration uses wire version 2 and the exact acknowledgement layout', () => {
    const frame = writeRegister('client', 'Browser');
    const view = new DataView(frame.buffer, frame.byteOffset, frame.byteLength);

    assert.equal(frame[0], 0x03);
    assert.equal(view.getUint16(1, true), WIRE_VERSION);
    assert.equal(view.getInt32(3, true), 6);
    assert.equal(view.getInt32(13, true), 7);

    assert.deepEqual(readRegisterAckDetails(new Uint8Array([0x04, 1, 2, 0])), {
        success: true,
        version: WIRE_VERSION,
    });
    assert.equal(readRegisterAck(new Uint8Array([0x04, 1, 2, 0])), true);
    assert.equal(readRegisterAckDetails(new Uint8Array([0x04, 1])), null);
    assert.equal(readRegisterAckDetails(new Uint8Array([0x04, 1, 2, 0, 9])), null);
    assert.equal(readRegisterAckDetails(new Uint8Array([0x04, 2, 2, 0])), null);
});

test('batches round-trip complete non-media frames as views', () => {
    const first = writeRequest(guid, 1, 2, 3, new Uint8Array([4]));
    const second = writeRequestCancel(guid);
    const batch = writeBatch([first, second]);
    const frames = readBatch(batch);

    assert.ok(frames);
    assert.equal(frames.length, 2);
    assert.equal(frames[0].buffer, batch.buffer);
    assert.deepEqual(Array.from(frames[0]), Array.from(first));
    assert.deepEqual(Array.from(frames[1]), Array.from(second));
});

test('batch validation rejects count, length, trailing, nesting, and media violations', () => {
    const frame = writeRequestCancel(guid);
    assert.throws(() => writeBatch([frame]), /2 to 32/);
    assert.throws(() => writeBatch(Array.from({ length: MAX_BATCH_FRAMES + 1 }, () => frame)), /2 to 32/);
    assert.throws(() => writeBatch([frame, new Uint8Array([0x21])]), /media/);
    assert.throws(() => writeBatch([frame, new Uint8Array([0x0c])]), /nested/);

    const valid = writeBatch([frame, frame]);
    const countZero = valid.slice();
    new DataView(countZero.buffer).setInt32(1, 0, true);
    assert.equal(readBatch(countZero), null);

    const negativeLength = valid.slice();
    new DataView(negativeLength.buffer).setInt32(5, -1, true);
    assert.equal(readBatch(negativeLength), null);

    const malformedRequest = writeRequest(guid, 1, 2, 3, new Uint8Array([1]));
    new DataView(malformedRequest.buffer).setInt32(29, 2, true);
    assert.throws(() => writeBatch([malformedRequest, frame]), /cannot contain/);

    const malformedBatch = writeBatch([writeRequest(guid, 1, 2, 3, new Uint8Array([1])), frame]);
    new DataView(malformedBatch.buffer).setInt32(5 + 4 + 29, 2, true);
    assert.equal(readBatch(malformedBatch), null);

    const mismatchedTopicHash = writePublish('events', false, new Uint8Array([1]));
    new DataView(mismatchedTopicHash.buffer).setInt32(1, 0, true);
    assert.throws(() => writeBatch([frame, mismatchedTopicHash]), /cannot contain/);
    const mismatchedTopicBatch = writeBatch([frame, writePublish('events', false, new Uint8Array([1]))]);
    new DataView(mismatchedTopicBatch.buffer).setInt32(5 + 4 + frame.length + 4 + 1, 0, true);
    assert.equal(readBatch(mismatchedTopicBatch), null);
    assert.equal(readBatch(valid.subarray(0, valid.length - 1)), null);
    assert.equal(readBatch(new Uint8Array([...valid, 0])), null);

    const oversized = new Uint8Array(MAX_BATCH_BYTES + 1);
    oversized[0] = 0x0c;
    assert.equal(readBatch(oversized), null);
});
