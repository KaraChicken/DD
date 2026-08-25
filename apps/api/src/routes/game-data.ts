import type { FastifyInstance } from 'fastify'
import { ensureGameSchema, getPlayer, getInventory, getEquipment, equipItem, getShop, buyItem, getActivities, getQuests, claimQuest } from '../infrastructure/game-db.js'
import { getSqliteDb } from '../infrastructure/sqlite.js'

async function userId(request: any, reply: any): Promise<number | null> {
  try {
    const payload = await request.jwtVerify<{ userId: number }>()
    return Number(payload.userId)
  } catch {
    reply.code(401).send({ message: '尚未登入', code: 'NOT_AUTHENTICATED' })
    return null
  }
}

function seedShop() {
  ensureGameSchema()
  const db = getSqliteDb()
  const count = db.prepare('SELECT COUNT(*) AS count FROM shop_items').get() as { count: number }
  if (count.count === 0) {
    db.prepare(`INSERT OR IGNORE INTO shop_items (ItemID, Price, Currency) SELECT ItemID, Price, 'gold' FROM items`).run()
  }
}

function ensureUserQuests(id: number) {
  ensureGameSchema()
  const db = getSqliteDb()
  db.prepare(`INSERT OR IGNORE INTO user_quests (UserID, QuestID, Progress) SELECT ?, QuestID, CASE WHEN GoalType = 'login' THEN 1 ELSE 0 END FROM quests WHERE Enabled = 1`).run(id)
}

export async function gameDataRoutes(app: FastifyInstance) {
  app.get('/api/player/me', async (request, reply) => {
    const id = await userId(request, reply)
    if (id === null) return
    const player = getPlayer(id)
    if (!player) return reply.code(404).send({ message: '玩家不存在', code: 'PLAYER_NOT_FOUND' })
    return { ok: true, player }
  })

  app.get('/api/inventory', async (request, reply) => {
    const id = await userId(request, reply)
    if (id === null) return
    return { ok: true, items: getInventory(id) }
  })

  app.get('/api/equipment', async (request, reply) => {
    const id = await userId(request, reply)
    if (id === null) return
    return { ok: true, slots: getEquipment(id) }
  })

  app.post('/api/equipment/equip', async (request, reply) => {
    const id = await userId(request, reply)
    if (id === null) return
    const body = request.body as { itemId?: number; slot?: string }
    if (!Number.isInteger(body?.itemId) || !body?.slot) return reply.code(400).send({ message: '裝備參數錯誤', code: 'INVALID_EQUIP_PAYLOAD' })
    if (!equipItem(id, Number(body.itemId), body.slot)) return reply.code(400).send({ message: '物品不存在或不在背包', code: 'ITEM_NOT_OWNED' })
    return { ok: true, slots: getEquipment(id) }
  })

  app.get('/api/shop/items', async (_request, reply) => {
    try { seedShop(); return { ok: true, items: getShop() } } catch (error) { reply.code(500); return { message: '商店初始化失敗', error: String(error) } }
  })

  app.post('/api/shop/buy', async (request, reply) => {
    const id = await userId(request, reply)
    if (id === null) return
    seedShop()
    const body = request.body as { itemId?: number; count?: number }
    if (!Number.isInteger(body?.itemId) || !Number.isInteger(body?.count)) return reply.code(400).send({ message: '購買參數錯誤', code: 'INVALID_PURCHASE_PAYLOAD' })
    const result = buyItem(id, Number(body.itemId), Number(body.count))
    if (!result.ok) return reply.code(400).send({ message: result.reason === 'INSUFFICIENT_FUNDS' ? '餘額不足' : '商品不存在', code: result.reason })
    return result
  })

  app.get('/api/activities', async (_request, reply) => {
    try { return { ok: true, activities: getActivities() } } catch (error) { return reply.code(500).send({ message: '活動載入失敗', error: String(error) }) }
  })

  app.get('/api/quests', async (request, reply) => {
    const id = await userId(request, reply)
    if (id === null) return
    ensureUserQuests(id)
    return { ok: true, quests: getQuests(id) }
  })

  app.post('/api/quests/:questId/claim', async (request, reply) => {
    const id = await userId(request, reply)
    if (id === null) return
    const questId = Number((request.params as { questId: string }).questId)
    if (!Number.isInteger(questId)) return reply.code(400).send({ message: '任務參數錯誤', code: 'INVALID_QUEST' })
    ensureUserQuests(id)
    const result = claimQuest(id, questId)
    if (!result.ok) return reply.code(400).send({ message: '任務尚未完成或已領取', code: result.reason })
    return { ok: true, quests: getQuests(id), inventory: getInventory(id) }
  })
}
