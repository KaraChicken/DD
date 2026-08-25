import { getSqliteDb } from './sqlite.js'

export function ensureGameSchema() {
  const db = getSqliteDb()
  db.exec(`
    CREATE TABLE IF NOT EXISTS items (
      ItemID INTEGER PRIMARY KEY AUTOINCREMENT,
      Name TEXT NOT NULL,
      Category TEXT NOT NULL,
      Price INTEGER NOT NULL DEFAULT 0,
      Attack INTEGER NOT NULL DEFAULT 0,
      Defence INTEGER NOT NULL DEFAULT 0,
      Agility INTEGER NOT NULL DEFAULT 0,
      Luck INTEGER NOT NULL DEFAULT 0,
      MaxCount INTEGER NOT NULL DEFAULT 99,
      Description TEXT NOT NULL DEFAULT ''
    );
    CREATE TABLE IF NOT EXISTS inventory (
      UserID INTEGER NOT NULL,
      ItemID INTEGER NOT NULL,
      Count INTEGER NOT NULL DEFAULT 0,
      StrengthenLevel INTEGER NOT NULL DEFAULT 0,
      IsEquipped INTEGER NOT NULL DEFAULT 0,
      PRIMARY KEY (UserID, ItemID),
      FOREIGN KEY (UserID) REFERENCES users(UserID) ON DELETE CASCADE,
      FOREIGN KEY (ItemID) REFERENCES items(ItemID) ON DELETE CASCADE
    );
    CREATE TABLE IF NOT EXISTS equipment_slots (
      UserID INTEGER NOT NULL,
      Slot TEXT NOT NULL,
      ItemID INTEGER,
      PRIMARY KEY (UserID, Slot),
      FOREIGN KEY (UserID) REFERENCES users(UserID) ON DELETE CASCADE,
      FOREIGN KEY (ItemID) REFERENCES items(ItemID) ON DELETE SET NULL
    );
    CREATE TABLE IF NOT EXISTS shop_items (
      ShopID INTEGER PRIMARY KEY AUTOINCREMENT,
      ItemID INTEGER NOT NULL UNIQUE,
      Price INTEGER NOT NULL,
      Currency TEXT NOT NULL DEFAULT 'gold',
      Enabled INTEGER NOT NULL DEFAULT 1,
      FOREIGN KEY (ItemID) REFERENCES items(ItemID) ON DELETE CASCADE
    );
    CREATE TABLE IF NOT EXISTS activities (
      ActivityID INTEGER PRIMARY KEY AUTOINCREMENT,
      Name TEXT NOT NULL,
      Description TEXT NOT NULL DEFAULT '',
      StartAt TEXT NOT NULL,
      EndAt TEXT NOT NULL,
      RewardItemID INTEGER,
      RewardCount INTEGER NOT NULL DEFAULT 0,
      Enabled INTEGER NOT NULL DEFAULT 1,
      FOREIGN KEY (RewardItemID) REFERENCES items(ItemID) ON DELETE SET NULL
    );
    CREATE TABLE IF NOT EXISTS quests (
      QuestID INTEGER PRIMARY KEY AUTOINCREMENT,
      Name TEXT NOT NULL,
      Description TEXT NOT NULL DEFAULT '',
      GoalType TEXT NOT NULL,
      GoalValue INTEGER NOT NULL DEFAULT 1,
      RewardGold INTEGER NOT NULL DEFAULT 0,
      RewardMoney INTEGER NOT NULL DEFAULT 0,
      RewardItemID INTEGER,
      RewardItemCount INTEGER NOT NULL DEFAULT 0,
      Enabled INTEGER NOT NULL DEFAULT 1,
      FOREIGN KEY (RewardItemID) REFERENCES items(ItemID) ON DELETE SET NULL
    );
    CREATE TABLE IF NOT EXISTS user_quests (
      UserID INTEGER NOT NULL,
      QuestID INTEGER NOT NULL,
      Progress INTEGER NOT NULL DEFAULT 0,
      Claimed INTEGER NOT NULL DEFAULT 0,
      PRIMARY KEY (UserID, QuestID),
      FOREIGN KEY (UserID) REFERENCES users(UserID) ON DELETE CASCADE,
      FOREIGN KEY (QuestID) REFERENCES quests(QuestID) ON DELETE CASCADE
    );
  `)

  const itemCount = db.prepare('SELECT COUNT(*) AS count FROM items').get() as { count: number }
  if (itemCount.count === 0) {
    const insert = db.prepare(`INSERT INTO items (Name, Category, Price, Attack, Defence, Agility, Luck, Description) VALUES (?, ?, ?, ?, ?, ?, ?, ?)`)
    const seed = db.transaction(() => {
      insert.run('木劍', 'weapon', 50, 5, 0, 0, 0, '新手武器')
      insert.run('皮甲', 'armor', 60, 0, 5, 0, 0, '基礎防具')
      insert.run('幸運戒指', 'ring', 80, 0, 0, 1, 3, '增加少量幸運')
      insert.run('小型補給箱', 'consumable', 20, 0, 0, 0, 0, '活動與任務常用道具')
    })
    seed()
  }

  const questCount = db.prepare('SELECT COUNT(*) AS count FROM quests').get() as { count: number }
  if (questCount.count === 0) {
    const insert = db.prepare(`INSERT INTO quests (Name, Description, GoalType, GoalValue, RewardGold, RewardMoney, RewardItemID, RewardItemCount) VALUES (?, ?, ?, ?, ?, ?, ?, ?)`)
    insert.run('每日登入', '登入遊戲一次', 'login', 1, 10, 0, 4, 1)
    insert.run('新手收藏家', '取得 3 件物品', 'collect_items', 3, 20, 50, null, 0)
  }

  const activityCount = db.prepare('SELECT COUNT(*) AS count FROM activities').get() as { count: number }
  if (activityCount.count === 0) {
    db.prepare(`INSERT INTO activities (Name, Description, StartAt, EndAt, RewardItemID, RewardCount) VALUES (?, ?, ?, ?, ?, ?)`)
      .run('新手登入活動', '首次登入即可領取補給箱', '2020-01-01T00:00:00Z', '2099-12-31T23:59:59Z', 4, 1)
  }
}

export function getPlayer(userId: number) {
  ensureGameSchema()
  return getSqliteDb().prepare(`SELECT UserID as userId, UserName as username, NickName as nickname, Sex as sex, Money as money, GiftToken as giftToken, Gold as gold, GP as gp, Grade as grade, Attack as attack, Defence as defence, Agility as agility, Luck as luck, WinCount as winCount, TotalCount as totalCount, EscapeCount as escapeCount, Repute as repute, ConsortiaID as consortiaId, ConsortiaName as consortiaName, Offer as offer, Skin as skin, Style as style, Colors as colors, FightPower as fightPower FROM users WHERE UserID = ?`).get(userId)
}

export function getInventory(userId: number) {
  ensureGameSchema()
  return getSqliteDb().prepare(`SELECT i.ItemID as itemId, i.Name as name, i.Category as category, i.Attack as attack, i.Defence as defence, i.Agility as agility, i.Luck as luck, i.Description as description, v.Count as count, v.StrengthenLevel as strengthenLevel, v.IsEquipped as isEquipped FROM inventory v JOIN items i ON i.ItemID = v.ItemID WHERE v.UserID = ? ORDER BY i.Category, i.ItemID`).all(userId)
}

export function getEquipment(userId: number) {
  ensureGameSchema()
  return getSqliteDb().prepare(`SELECT e.Slot as slot, i.ItemID as itemId, i.Name as name, i.Category as category, i.Attack as attack, i.Defence as defence, i.Agility as agility, i.Luck as luck, v.StrengthenLevel as strengthenLevel FROM equipment_slots e LEFT JOIN items i ON i.ItemID = e.ItemID LEFT JOIN inventory v ON v.UserID = e.UserID AND v.ItemID = e.ItemID WHERE e.UserID = ? ORDER BY e.Slot`).all(userId)
}

export function equipItem(userId: number, itemId: number, slot: string) {
  ensureGameSchema()
  const db = getSqliteDb()
  const item = db.prepare('SELECT Category FROM items WHERE ItemID = ?').get(itemId) as { Category: string } | undefined
  const owned = db.prepare('SELECT Count FROM inventory WHERE UserID = ? AND ItemID = ?').get(userId, itemId) as { Count: number } | undefined
  if (!item || !owned || owned.Count < 1) return false
  db.prepare(`INSERT INTO equipment_slots (UserID, Slot, ItemID) VALUES (?, ?, ?) ON CONFLICT(UserID, Slot) DO UPDATE SET ItemID=excluded.ItemID`).run(userId, slot, itemId)
  db.prepare('UPDATE inventory SET IsEquipped = 0 WHERE UserID = ? AND ItemID IN (SELECT ItemID FROM equipment_slots WHERE UserID = ? AND Slot = ? AND ItemID IS NOT NULL)').run(userId, userId, slot)
  db.prepare('UPDATE inventory SET IsEquipped = 1 WHERE UserID = ? AND ItemID = ?').run(userId, itemId)
  return true
}

export function getShop() {
  ensureGameSchema()
  return getSqliteDb().prepare(`SELECT s.ShopID as shopId, i.ItemID as itemId, i.Name as name, i.Category as category, i.Attack as attack, i.Defence as defence, i.Agility as agility, i.Luck as luck, s.Price as price, s.Currency as currency, i.Description as description FROM shop_items s JOIN items i ON i.ItemID = s.ItemID WHERE s.Enabled = 1 ORDER BY s.ShopID`).all()
}

export function buyItem(userId: number, itemId: number, count: number) {
  ensureGameSchema()
  const db = getSqliteDb()
  const row = db.prepare('SELECT Price, Currency FROM shop_items WHERE ItemID = ? AND Enabled = 1').get(itemId) as { Price: number; Currency: string } | undefined
  const item = db.prepare('SELECT MaxCount FROM items WHERE ItemID = ?').get(itemId) as { MaxCount: number } | undefined
  if (!row || !item || !Number.isInteger(count) || count < 1) return { ok: false, reason: 'INVALID_ITEM' }
  const total = row.Price * count
  const column = row.Currency === 'money' ? 'Money' : 'Gold'
  const user = db.prepare(`SELECT ${column} AS balance FROM users WHERE UserID = ?`).get(userId) as { balance: number } | undefined
  if (!user || user.balance < total) return { ok: false, reason: 'INSUFFICIENT_FUNDS' }
  const tx = db.transaction(() => {
    db.prepare(`UPDATE users SET ${column} = ${column} - ? WHERE UserID = ?`).run(total, userId)
    db.prepare(`INSERT INTO inventory (UserID, ItemID, Count) VALUES (?, ?, ?) ON CONFLICT(UserID, ItemID) DO UPDATE SET Count = MIN(Count + excluded.Count, ?)`).run(userId, itemId, count, item.MaxCount)
  })
  tx()
  return { ok: true, total, currency: row.Currency }
}

export function getActivities() {
  ensureGameSchema()
  return getSqliteDb().prepare(`SELECT a.ActivityID as activityId, a.Name as name, a.Description as description, a.StartAt as startAt, a.EndAt as endAt, a.RewardItemID as rewardItemId, i.Name as rewardItemName, a.RewardCount as rewardCount FROM activities a LEFT JOIN items i ON i.ItemID = a.RewardItemID WHERE a.Enabled = 1 ORDER BY a.StartAt DESC`).all()
}

export function getQuests(userId: number) {
  ensureGameSchema()
  return getSqliteDb().prepare(`SELECT q.QuestID as questId, q.Name as name, q.Description as description, q.GoalType as goalType, q.GoalValue as goalValue, COALESCE(u.Progress, 0) as progress, COALESCE(u.Claimed, 0) as claimed, q.RewardGold as rewardGold, q.RewardMoney as rewardMoney, q.RewardItemID as rewardItemId, i.Name as rewardItemName, q.RewardItemCount as rewardItemCount FROM quests q LEFT JOIN user_quests u ON u.QuestID = q.QuestID AND u.UserID = ? LEFT JOIN items i ON i.ItemID = q.RewardItemID WHERE q.Enabled = 1 ORDER BY q.QuestID`).all(userId)
}

export function claimQuest(userId: number, questId: number) {
  ensureGameSchema()
  const db = getSqliteDb()
  const q = db.prepare('SELECT * FROM quests WHERE QuestID = ? AND Enabled = 1').get(questId) as any
  const state = db.prepare('SELECT Progress, Claimed FROM user_quests WHERE UserID = ? AND QuestID = ?').get(userId, questId) as any
  if (!q || !state || state.Claimed || state.Progress < q.GoalValue) return { ok: false, reason: 'QUEST_NOT_READY' }
  const tx = db.transaction(() => {
    db.prepare('UPDATE user_quests SET Claimed = 1 WHERE UserID = ? AND QuestID = ?').run(userId, questId)
    db.prepare('UPDATE users SET Gold = Gold + ?, Money = Money + ? WHERE UserID = ?').run(q.RewardGold, q.RewardMoney, userId)
    if (q.RewardItemID && q.RewardItemCount > 0) db.prepare(`INSERT INTO inventory (UserID, ItemID, Count) VALUES (?, ?, ?) ON CONFLICT(UserID, ItemID) DO UPDATE SET Count = Count + excluded.Count`).run(userId, q.RewardItemID, q.RewardItemCount)
  })
  tx()
  return { ok: true }
}
