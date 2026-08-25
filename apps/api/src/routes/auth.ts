import { z } from 'zod'
import type { FastifyInstance } from 'fastify'

const loginSchema = z.object({
  username: z.string().min(1).max(64),
  password: z.string().min(1).max(256),
})

export async function authRoutes(app: FastifyInstance) {
  app.post('/api/auth/login', async (request, reply) => {
    const result = loginSchema.safeParse(request.body)

    if (!result.success) {
      return reply.code(400).send({ message: 'Invalid login payload' })
    }

    // The legacy Web implementation authenticates against SQL Server.
    // Do not connect the new API directly to that database here yet.
    // The next persistence step will introduce an AccountRepository/SaveStore.
    request.log.info({ username: result.data.username }, 'login contract reached')

    return reply.code(501).send({
      message: 'Authentication backend is not connected yet',
      code: 'AUTH_NOT_IMPLEMENTED',
    })
  })

  app.post('/api/auth/logout', async () => ({ ok: true }))
}
