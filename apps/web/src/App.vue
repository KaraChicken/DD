<script setup lang="ts">
const legacyWebPath = "/Web/";
</script>

<template>
  <main class="shell">
    <header class="header">
      <div>
        <p class="eyebrow">DDTank Web</p>
        <h1>Modern Web</h1>
      </div>
      <span class="badge">Vue 3</span>
    </header>

    <section class="card">
      <h2>Web 重構已啟動</h2>
      <p>
        新 Web 與舊 PHP Web 並存。Flash、遊戲 Server 與既有協定目前完全不動。
      </p>
      <p class="muted">
        下一階段會逐項遷移登入、玩家資料、背包、商城與其他 Web 功能。
      </p>
      <a :href="legacyWebPath">舊版 Web</a>
    </section>
  </main>
</template>
