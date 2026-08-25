import { apiFetch } from './client'

export type GamePlayer = { userId:number; username:string; nickname:string; sex:number; money:number; giftToken:number; gold:number; gp:number; grade:number; attack:number; defence:number; agility:number; luck:number; fightPower:number }
export type GameItem = { itemId:number; name:string; category:string; attack:number; defence:number; agility:number; luck:number; description:string; count:number; strengthenLevel:number; isEquipped:number }
export type EquipmentSlot = { slot:string; itemId:number|null; name:string|null; category:string|null; attack:number|null; defence:number|null; agility:number|null; luck:number|null; strengthenLevel:number|null }
export type ShopItem = { shopId:number; itemId:number; name:string; category:string; attack:number; defence:number; agility:number; luck:number; price:number; currency:string; description:string }
export type Activity = { activityId:number; name:string; description:string; startAt:string; endAt:string; rewardItemId:number|null; rewardItemName:string|null; rewardCount:number }
export type Quest = { questId:number; name:string; description:string; goalType:string; goalValue:number; progress:number; claimed:number; rewardGold:number; rewardMoney:number; rewardItemName:string|null; rewardItemCount:number }

export const getPlayer = () => apiFetch<{ok:true;player:GamePlayer}>('/api/player/me')
export const getInventory = () => apiFetch<{ok:true;items:GameItem[]}>('/api/inventory')
export const getEquipment = () => apiFetch<{ok:true;slots:EquipmentSlot[]}>('/api/equipment')
export const equipItem = (itemId:number, slot:string) => apiFetch<{ok:true;slots:EquipmentSlot[]}>('/api/equipment/equip', {method:'POST', body:JSON.stringify({itemId,slot})})
export const getShop = () => apiFetch<{ok:true;items:ShopItem[]}>('/api/shop/items')
export const buyItem = (itemId:number, count=1) => apiFetch<{ok:true}>('/api/shop/buy', {method:'POST', body:JSON.stringify({itemId,count})})
export const getActivities = () => apiFetch<{ok:true;activities:Activity[]}>('/api/activities')
export const getQuests = () => apiFetch<{ok:true;quests:Quest[]}>('/api/quests')
export const claimQuest = (questId:number) => apiFetch<{ok:true}>('/api/quests/'+questId+'/claim', {method:'POST'})
