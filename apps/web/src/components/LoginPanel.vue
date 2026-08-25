<script setup lang="ts">
import { ref } from 'vue'

const emit = defineEmits<{
  login: [username: string, password: string]
  register: []
  recover: []
}>()

const username = ref('')
const password = ref('')

function submit() {
  emit('login', username.value, password.value)
}
</script>

<template>
  <div class="unlogin-box clearfix">
    <form method="POST" action="" @submit.prevent="submit">
      <div class="input-box f-l p-r">
        <input v-model="username" type="text" name="UserName" placeholder="Usuario" autocomplete="username" />
        <input v-model="password" type="password" name="Password" placeholder="Senha" autocomplete="current-password" />
        <div class="blank"></div>
      </div>
      <button class="loginbtn f-l" type="submit" name="login"></button>
    </form>
    <div class="forget f-l">
      <a href="#" @click.prevent="emit('register')">Registrar conta</a> |
      <a href="#" @click.prevent="emit('recover')">Recuperar senha?</a>
    </div>
  </div>
</template>
