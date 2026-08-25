<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { getActivities, getEquipment, getInventory, getPlayer, getQuests, getShop, buyItem, claimQuest, equipItem, type Activity, type EquipmentSlot, type GameItem, type GamePlayer, type Quest, type ShopItem } from '../api/game'

const player = ref<GamePlayer | null>(null)
const inventory = ref<GameItem[]>([])
const equipment = ref<EquipmentSlot[]>([])
const shop = ref<ShopItem[]>([])
const activities = ref<Activity[]>([])
const quests = ref<Quest[]>([])
const tab = ref('player')
const error = ref('')

async function load() {
  error.value = ''
  try {
    const [p,i,e,s,a,q] = await Promise.all([getPlayer(),getInventory(),getEquipment(),getShop(),getActivities(),getQuests()])
    player.value=p.player; inventory.value=i.items; equipment.value=e.slots; shop.value=s.items; activities.value=a.activities; quests.value=q.quests
  } catch (e) { error.value = e instanceof Error ? e.message : '載入失敗' }
}
async function buy(itemId:number) { await buyItem(itemId); await load() }
async function equip(item:GameItem) { const slot = item.category === 'weapon' ? 'weapon' : item.category === 'armor' ? 'armor' : 'ring'; await equipItem(item.itemId, slot); await load() }
async function claim(id:number) { await claimQuest(id); await load() }
onMounted(load)
</script>

<template>
  <main class="page">
    <header><div><h1>{{ player?.nickname ?? '玩家' }}</h1><span>Lv. {{ player?.grade ?? '-' }} · 戰力 {{ player?.fightPower ?? 0 }}</span></div><button @click="load">重新整理</button></header>
    <p v-if="error" class="error">{{ error }}</p>
    <nav><button v-for="item in [['player','玩家'],['inventory','背包'],['equipment','裝備'],['shop','商店'],['activities','活動'],['quests','任務']]" :key="item[0]" :class="{active:tab===item[0]}" @click="tab=item[0]">{{ item[1] }}</button></nav>
    <section v-if="tab==='player'" class="grid"><article><h2>資源</h2><p>Gold：{{ player?.gold }}</p><p>Money：{{ player?.money }}</p><p>GiftToken：{{ player?.giftToken }}</p><p>GP：{{ player?.gp }}</p></article><article><h2>屬性</h2><p>攻擊 {{ player?.attack }} · 防禦 {{ player?.defence }}</p><p>敏捷 {{ player?.agility }} · 幸運 {{ player?.luck }}</p></article></section>
    <section v-else-if="tab==='inventory'" class="grid"><article v-for="item in inventory" :key="item.itemId"><h2>{{ item.name }}</h2><p>{{ item.description }}</p><p>x{{ item.count }} · 強化 +{{ item.strengthenLevel }}</p><button @click="equip(item)">裝備</button></article></section>
    <section v-else-if="tab==='equipment'" class="grid"><article v-for="slot in equipment" :key="slot.slot"><h2>{{ slot.slot }}</h2><p>{{ slot.name ?? '空' }}</p><p v-if="slot.itemId">攻 {{ slot.attack }} / 防 {{ slot.defence }} / 敏 {{ slot.agility }} / 幸 {{ slot.luck }}</p></article></section>
    <section v-else-if="tab==='shop'" class="grid"><article v-for="item in shop" :key="item.itemId"><h2>{{ item.name }}</h2><p>{{ item.description }}</p><p>{{ item.price }} {{ item.currency }}</p><button @click="buy(item.itemId)">購買</button></article></section>
    <section v-else-if="tab==='activities'" class="grid"><article v-for="item in activities" :key="item.activityId"><h2>{{ item.name }}</h2><p>{{ item.description }}</p><p>獎勵：{{ item.rewardItemName ?? '無' }} x{{ item.rewardCount }}</p></article></section>
    <section v-else class="grid"><article v-for="item in quests" :key="item.questId"><h2>{{ item.name }}</h2><p>{{ item.description }}</p><p>{{ item.progress }} / {{ item.goalValue }}</p><p>獎勵：{{ item.rewardGold }} Gold、{{ item.rewardMoney }} Money、{{ item.rewardItemName ?? '無' }}</p><button :disabled="item.claimed===1 || item.progress < item.goalValue" @click="claim(item.questId)">{{ item.claimed ? '已領取' : item.progress >= item.goalValue ? '領取' : '進行中' }}</button></article></section>
  </main>
</template>

<style scoped>
.page{max-width:1100px;margin:auto;padding:32px}header,nav{display:flex;gap:10px;align-items:center;justify-content:space-between;margin-bottom:20px}nav{justify-content:flex-start;flex-wrap:wrap}button{padding:9px 14px;border:1px solid #ccc;border-radius:8px;background:#fff;cursor:pointer}.active{font-weight:700}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:14px}article{border:1px solid #ddd;border-radius:12px;padding:18px}.error{color:#b42318}
</style>
