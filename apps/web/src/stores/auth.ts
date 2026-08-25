import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { apiFetch } from '../api/client'

export interface AuthUser {
  id: number
  username: string
  nickname?: string
}

interface LoginResponse {
  user: AuthUser
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<AuthUser | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const isLoggedIn = computed(() => user.value !== null)

  async function login(username: string, password: string) {
    loading.value = true
    error.value = null

    try {
      const result = await apiFetch<LoginResponse>('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({ username, password }),
      })
      user.value = result.user
      return true
    } catch (cause) {
      error.value = cause instanceof Error ? cause.message : 'Login failed'
      return false
    } finally {
      loading.value = false
    }
  }

  async function logout() {
    await apiFetch<void>('/api/auth/logout', { method: 'POST' })
    user.value = null
  }

  return { user, loading, error, isLoggedIn, login, logout }
})
