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
 * The old PHP code calls Mem_Users_Accede positionally with:
 *   DanDanTang, UserName, UPPER(MD5(password)), UserID OUT
 *
 * Keep that positional contract here instead of guessing the stored
 * procedure's parameter names. This is a compatibility boundary only.
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
  const result = await pool.request()
    .input('ApplicationName', sql.VarChar(64), 'DanDanTang')
    .input('UserName', sql.VarChar(64), username)
    .input('Password', sql.VarChar(64), passwordMd5)
    .query<{ UserID: number | null }>(`
      DECLARE @UserID INT;

      EXEC Mem_Users_Accede
        @ApplicationName,
        @UserName,
        @Password,
        @UserID OUTPUT;

      SELECT @UserID AS UserID;
    `)

  const userId = Number(result.recordset[0]?.UserID)
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
