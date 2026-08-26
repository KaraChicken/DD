import Fastify from 'fastify'
import cors from '@fastify/cors'
import cookie from '@fastify/cookie'
import jwt from '@fastify/jwt'
import { env, requireSessionSecret } from './config.js'
import { authRoutes } from './routes/auth.js'
import { accountRoutes } from './routes/account.js'
import { gameDataRoutes } from './routes/game-data.js'
import { flashProtocolRoutes } from './routes/flash-protocol.js'
import { FlashGameServer } from './flash/server.js'

const app = Fastify({ logger: true })

await app.register(cors, { origin: true, credentials: true })
await app.register(cookie)
await app.register(jwt, {
  secret: requireSessionSecret(),
  cookie: { cookieName: 'dd_session', signed: false },
})
await app.register(authRoutes)
await app.register(accountRoutes)
await app.register(gameDataRoutes)
await app.register(flashProtocolRoutes)

app.get('/health', async () => ({
  status: 'ok',
  service: 'dd-api',
  environment: env.NODE_ENV,
  flashSocket: env.FLASH_SOCKET_ENABLED,
}))

const flashServer = env.FLASH_SOCKET_ENABLED
  ? new FlashGameServer({
      host: env.FLASH_HOST,
      port: env.FLASH_PORT,
      encrypted: env.FLASH_SOCKET_ENCRYPTED,
    })
  : null

if (flashServer) await flashServer.listen()
await app.listen({ port: env.PORT, host: env.HOST })

const shutdown = async () => {
  if (flashServer) await flashServer.close()
  await app.close()
}

process.once('SIGINT', shutdown)
process.once('SIGTERM', shutdown)
