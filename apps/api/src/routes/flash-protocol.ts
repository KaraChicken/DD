import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify'
import { ensureGameSchema, getActivities, getEquipment, getInventory, getQuests, getShop, getPlayer } from '../infrastructure/game-db.js'

function escapeXml(value: unknown): string {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&apos;')
}

function attr(name: string, value: unknown): string {
  return `${name}="${escapeXml(value)}"`
}

function xmlResult(body: string, ok = true, message = 'Success!'): string {
  return `<Result ${attr('value', ok)} ${attr('message', message)}>${body}</Result>`
}

function sendXml(reply: FastifyReply, xml: string) {
  return reply.header('content-type', 'text/plain; charset=utf-8').send(xml)
}

async function sessionUserId(request: FastifyRequest, reply: FastifyReply): Promise<number | null> {
  try {
    const payload = await request.jwtVerify<{ userId: number }>()
    return Number(payload.userId)
  } catch {
    sendXml(reply.code(401), xmlResult('', false, 'Not authenticated!'))
    return null
  }
}

function requestedUserId(request: FastifyRequest, authenticatedId: number): number {
  const query = request.query as Record<string, string | undefined>
  const id = Number(query.ID ?? query.id ?? authenticatedId)
  return Number.isInteger(id) && id > 0 ? id : authenticatedId
}

function goodsXml(item: Record<string, unknown>): string {
  return `<Item ${attr('ItemID', item.itemId)} ${attr('Name', item.name)} ${attr('Category', item.category)} ${attr('Count', item.count)} ${attr('StrengthenLevel', item.strengthenLevel)} ${attr('IsEquiped', item.isEquipped ? 1 : 0)} ${attr('Attack', item.attack)} ${attr('Defence', item.defence)} ${attr('Agility', item.agility)} ${attr('Luck', item.luck)} ${attr('Description', item.description)} />`
}

export async function flashProtocolRoutes(app: FastifyInstance) {
  ensureGameSchema()

  // Legacy endpoint: /Request/LoadUserItems.ashx?ID=123
  app.get('/Request/LoadUserItems.ashx', async (request, reply) => {
    const authId = await sessionUserId(request, reply)
    if (authId === null) return
    const userId = requestedUserId(request, authId)
    if (userId !== authId) return sendXml(reply.code(403), xmlResult('', false, 'Invalid user!'))
    const items = getInventory(userId)
    return sendXml(reply, xmlResult(items.map((item) => goodsXml(item as Record<string, unknown>)).join('')))
  })

  // Legacy endpoint: /Request/LoadUserEquip.ashx?ID=123
  app.get('/Request/LoadUserEquip.ashx', async (request, reply) => {
    const authId = await sessionUserId(request, reply)
    if (authId === null) return
    const userId = requestedUserId(request, authId)
    if (userId !== authId) return sendXml(reply.code(403), xmlResult('', false, 'Invalid user!'))
    const player = getPlayer(userId) as Record<string, unknown> | undefined
    if (!player) return sendXml(reply.code(404), xmlResult('', false, 'Player not found!'))
    const attrs = ['agility', 'attack', 'colors', 'skin', 'defence', 'gp', 'grade', 'luck', 'repute', 'offer', 'nickname', 'consortiaName', 'consortiaId', 'winCount', 'totalCount', 'escapeCount', 'sex', 'style', 'fightPower']
      .map((key) => attr(key[0].toUpperCase() + key.slice(1), player[key]))
      .join(' ')
    const equipment = getEquipment(userId)
    return sendXml(reply, xmlResult(`<Player ${attrs}>${equipment.map((item) => goodsXml(item as Record<string, unknown>)).join('')}</Player>`))
  })

  // Legacy endpoint: /Request/ShopItemList.ashx
  app.get('/Request/ShopItemList.ashx', async (_request, reply) => {
    const items = getShop()
    const store = items.map((item) => `<Item ${attr('ItemID', item.itemId)} ${attr('Name', item.name)} ${attr('Category', item.category)} ${attr('Price', item.price)} ${attr('Currency', item.currency)} ${attr('Attack', item.attack)} ${attr('Defence', item.defence)} ${attr('Agility', item.agility)} ${attr('Luck', item.luck)} ${attr('Description', item.description)} />`).join('')
    return sendXml(reply, xmlResult(`<Store>${store}</Store>`))
  })

  // First protocol representation of QuestList. This intentionally stays uncompressed until the
  // original Flash decompression format is confirmed from the SWF/request code.
  app.get('/Request/QuestList.ashx', async (_request, reply) => {
    const quests = getQuests(0)
    const body = quests.map((quest) => `<Quest ${attr('QuestID', quest.questId)} ${attr('Name', quest.name)} ${attr('Description', quest.description)} ${attr('GoalType', quest.goalType)} ${attr('GoalValue', quest.goalValue)} ${attr('RewardGold', quest.rewardGold)} ${attr('RewardMoney', quest.rewardMoney)} ${attr('RewardItemID', quest.rewardItemId)} ${attr('RewardItemCount', quest.rewardItemCount)} />`).join('')
    return sendXml(reply, xmlResult(body))
  })

  // First protocol representation of ActiveList.
  app.get('/Request/ActiveList.ashx', async (_request, reply) => {
    const activities = getActivities()
    const body = activities.map((activity) => `<Active ${attr('ActivityID', activity.activityId)} ${attr('Name', activity.name)} ${attr('Description', activity.description)} ${attr('StartAt', activity.startAt)} ${attr('EndAt', activity.endAt)} ${attr('RewardItemID', activity.rewardItemId)} ${attr('RewardCount', activity.rewardCount)} />`).join('')
    return sendXml(reply, xmlResult(body))
  })
}
