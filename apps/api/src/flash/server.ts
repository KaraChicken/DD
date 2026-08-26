import net from 'node:net'
import { env } from '../config.js'
import { FLASH_DEFAULT_KEY, FlashPacketType, createPacket, encodeEncrypted, tryReadPacket, type FlashPacket } from './protocol.js'

export interface FlashSocketOptions {
  host?: string
  port?: number
  encrypted?: boolean
}

interface FlashClient {
  socket: net.Socket
  buffer: Buffer
  key: number[]
  encrypted: boolean
  authenticatedUserId?: number
}

export class FlashGameServer {
  private readonly server: net.Server
  private readonly host: string
  private readonly port: number
  private readonly encrypted: boolean
  private readonly clients = new Set<FlashClient>()

  constructor(options: FlashSocketOptions = {}) {
    this.host = options.host ?? process.env.FLASH_HOST ?? env.HOST
    this.port = options.port ?? Number(process.env.FLASH_PORT ?? 3250)
    this.encrypted = options.encrypted ?? process.env.FLASH_SOCKET_ENCRYPTED !== 'false'
    this.server = net.createServer((socket) => this.accept(socket))
  }

  listen(): Promise<void> {
    return new Promise((resolve, reject) => {
      const onError = (error: Error) => {
        this.server.off('listening', onListening)
        reject(error)
      }
      const onListening = () => {
        this.server.off('error', onError)
        resolve()
      }
      this.server.once('error', onError)
      this.server.once('listening', onListening)
      this.server.listen(this.port, this.host)
    })
  }

  async close(): Promise<void> {
    for (const client of this.clients) client.socket.destroy()
    await new Promise<void>((resolve) => this.server.close(() => resolve()))
  }

  private accept(socket: net.Socket) {
    const client: FlashClient = {
      socket,
      buffer: Buffer.alloc(0),
      key: [...FLASH_DEFAULT_KEY],
      encrypted: this.encrypted,
    }
    this.clients.add(client)

    socket.on('data', (chunk) => this.onData(client, chunk))
    socket.on('close', () => this.clients.delete(client))
    socket.on('error', () => this.clients.delete(client))
  }

  private onData(client: FlashClient, chunk: Buffer) {
    client.buffer = Buffer.concat([client.buffer, chunk])
    while (client.buffer.length >= 4) {
      const decoded = tryReadPacket(client.buffer, client.encrypted, client.key)
      if (!decoded.packet) {
        client.buffer = decoded.rest
        return
      }
      client.buffer = decoded.rest
      this.handlePacket(client, decoded.packet)
    }
  }

  private handlePacket(client: FlashClient, packet: FlashPacket) {
    switch (packet.code) {
      case FlashPacketType.PING:
        this.send(client, FlashPacketType.PING)
        break
      case FlashPacketType.LOGIN:
        // Login is intentionally isolated from the generic packet decoder. The Flash client
        // sends an RSA-encrypted login body and then switches to the per-connection 8-byte key.
        // Implementing that handshake requires the original RSA key exchange and AccountInfo
        // flow to be mapped before accepting a user as authenticated.
        this.sendSystemMessage(client, 'Flash login handshake is not implemented yet.')
        break
      default:
        // Do not silently mutate game state for unknown packets. The next protocol slices will
        // map each opcode against GameSocketOut and the matching C# handler before enabling it.
        break
    }
  }

  private send(client: FlashClient, code: number, body = Buffer.alloc(0)) {
    const packet = createPacket(code, body)
    const wire = client.encrypted ? encodeEncrypted(packet, client.key) : packet
    client.socket.write(wire)
  }

  private sendSystemMessage(client: FlashClient, message: string) {
    const body = Buffer.alloc(4 + 2 + Buffer.byteLength(message, 'utf8'))
    body.writeInt32BE(0, 0)
    const text = Buffer.from(message, 'utf8')
    body.writeUInt16BE(text.length, 4)
    text.copy(body, 6)
    this.send(client, FlashPacketType.SYS_MESSAGE, body)
  }
}
