import { createHash } from 'node:crypto'
import sql from 'mssql'
import { getSqlPool } from '../infrastructure/sqlserver.js'

export interface AuthenticatedAccount {
  userId: number
  username: string
  email: string | null
}

/**
 * Mirrors the legacy Web authentication contract.
 * The old PHP code sends an upper-case MD5 password to Mem_Users_Accede.
 * This is intentionally kept at the legacy boundary so the existing
 * account database and Flash game remain compatible.
 */
export async function authenticateLegacyAccount(
  username: string,
  password: string,
): Promise<AuthenticatedAccount | null> {
  const passwordMd5 = createHash('md5')
    .update(password, 'utf8')
    .digest('hex')
    .toUpperCase()

  const pool = await getSqlPool()
  const request = pool.request()
    .input('ApplicationName', sql.VarChar(64), 'DanDanTang')
    .input('UserName', sql.VarChar(64), username)
    .input('Password', sql.VarChar(64), passwordMd5)
    .output('UserID', sql.Int)

  await request.execute('Mem_Users_Accede')

  const userId = Number(request.parameters.UserID.value)
  if (!Number.isInteger(userId) || userId < 0) {
    return null
  }

  const account = await pool.request()
    .input('UserID', sql.Int, userId)
    .query<{ Email: string | null }>(`
      SELECT TOP 1 Email
      FROM Mem_UserInfo
      WHERE UserID = @UserID
    `)

  return {
    userId,
    username,
    email: account.recordset[0]?.Email ?? null,
  }
}
