import { Buffer } from 'node:buffer'

export const FLASH_HEADER = 29099
export const FLASH_HEADER_SIZE = 20
export const FLASH_DEFAULT_KEY = [174, 191, 86, 120, 171, 205, 239, 241]

export const FlashPacketType = {
  LOGIN: 1,
  SYS_MESSAGE: 3,
  PING: 4,
  RSAKEY: 7,
  DAILY_AWARD: 13,
  SCENE_LOGIN: 16,
  SCENE_ADD_USER: 18,
  SCENE_CHAT: 19,
  SCENE_FACE: 20,
  SCENE_REMOVE_USER: 21,
  BAG_LOCKED: 25,
  DELETE_GOODS: 42,
  BUY_GOODS: 44,
  UPDATE_COUPONS: 46,
  UNCHAIN_EQUIP: 47,
  SELL_GOODS: 48,
  CHANGE_PLACE_GOODS: 49,
  FIGHT_NPC: 50,
  UPDATE_GOODS: 51,
  CHAIN_EQUIP: 52,
  ITEM_COMPOSE: 58,
  ITEM_STRENGTHEN: 59,
  ITEM_TRANSFER: 61,
  ITEM_CONTINUE: 62,
  ITEM_OPENUP: 63,
  ITEM_EQUIP: 74,
  ITEM_STORE: 79,
  GAME_ROOM: 94,
  GAME_CMD: 91,
  QUEST_ADD: 176,
  QUEST_REMOVE: 177,
  QUEST_UPDATE: 178,
  QUEST_FINISH: 179,
  QUEST_OBTAIN: 180,
  QUEST_CHECK: 181,
} as const

export interface FlashPacket {
  code: number
  clientId: number
  extend1: number
  extend2: number
  body: Buffer
  length: number
  checksum: number
}

export function checksum(packet: Buffer): number {
  let value = 119
  for (let i = 6; i < packet.length; i += 1) value += packet[i]
  return value & 32639
}

export function createPacket(code: number, body: Buffer = Buffer.alloc(0), clientId = 0, extend1 = 0, extend2 = 0): Buffer {
  const packet = Buffer.alloc(FLASH_HEADER_SIZE + body.length)
  packet.writeInt16BE(FLASH_HEADER, 0)
  packet.writeUInt16BE(packet.length, 2)
  packet.writeUInt16BE(0, 4)
  packet.writeInt16BE(code, 6)
  packet.writeInt32BE(clientId, 8)
  packet.writeInt32BE(extend1, 12)
  packet.writeInt32BE(extend2, 16)
  body.copy(packet, FLASH_HEADER_SIZE)
  packet.writeUInt16BE(checksum(packet), 4)
  return packet
}

export function encodeEncrypted(packet: Buffer, key: number[]): Buffer {
  const out = Buffer.from(packet)
  const state = key.slice(0, 8)
  for (let i = 0; i < out.length; i += 1) {
    if (i === 0) {
      out[0] = out[0] ^ state[0]
    } else {
      state[i % 8] = (state[i % 8] + out[i - 1]) ^ i
      out[i] = (out[i] ^ state[i % 8]) + out[i - 1]
      out[i] &= 0xff
    }
  }
  return out
}

export function decodeEncrypted(packet: Buffer, key: number[]): Buffer {
  const encrypted = Buffer.from(packet)
  const out = Buffer.alloc(encrypted.length)
  const state = key.slice(0, 8)
  for (let i = 0; i < encrypted.length; i += 1) {
    if (i === 0) {
      out[0] = encrypted[0] ^ state[0]
    } else {
      state[i % 8] = (state[i % 8] + encrypted[i - 1]) ^ i
      out[i] = (encrypted[i] - encrypted[i - 1]) ^ state[i % 8]
      out[i] &= 0xff
    }
  }
  return out
}

export function tryReadPacket(buffer: Buffer, encrypted: boolean, key: number[]): { packet?: FlashPacket; rest: Buffer } {
  let offset = 0
  while (buffer.length - offset >= 4) {
    const header = buffer.subarray(offset, offset + 4)
    const plainHeader = encrypted ? decodeEncrypted(header, key) : header
    if (plainHeader.readInt16BE(0) !== FLASH_HEADER) {
      offset += 1
      continue
    }

    const length = plainHeader.readUInt16BE(2)
    if (length < FLASH_HEADER_SIZE) {
      offset += 1
      continue
    }
    if (buffer.length - offset < length) return { rest: buffer.subarray(offset) }

    const raw = buffer.subarray(offset, offset + length)
    const plain = encrypted ? decodeEncrypted(raw, key) : Buffer.from(raw)
    const packet: FlashPacket = {
      code: plain.readInt16BE(6),
      clientId: plain.readInt32BE(8),
      extend1: plain.readInt32BE(12),
      extend2: plain.readInt32BE(16),
      body: plain.subarray(FLASH_HEADER_SIZE),
      length,
      checksum: plain.readUInt16BE(4),
    }
    return { packet, rest: buffer.subarray(offset + length) }
  }
  return { rest: buffer.subarray(offset) }
}

export function writeUtf(value: string): Buffer {
  const data = Buffer.from(value, 'utf8')
  const result = Buffer.alloc(2 + data.length)
  result.writeUInt16BE(data.length, 0)
  data.copy(result, 2)
  return result
}

export function readUtf(body: Buffer, offset = 0): { value: string; offset: number } {
  if (offset + 2 > body.length) throw new Error('Flash packet: missing UTF length')
  const length = body.readUInt16BE(offset)
  offset += 2
  if (offset + length > body.length) throw new Error('Flash packet: truncated UTF string')
  return { value: body.subarray(offset, offset + length).toString('utf8'), offset: offset + length }
}
