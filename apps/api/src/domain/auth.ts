import { createHash } from 'node:crypto'
import sql from 'mssql'
import { getSqlPool } from '../infrastructure/sqlserver.js'

export interface AuthenticatedAccount {
  userId: number
  username: string
  nickname: string
  email: string | null
}

export interface RegistrationInput {
  username: string
  password: string
  nickname: string
  sex: boolean
}

export type RegistrationFailure = 'USERNAME_EXISTS' | 'REGISTER_FAILED'

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
    .query<{ Email: string | null; NickName: string | null }>(`
      SELECT TOP 1 Email, NickName
      FROM Mem_UserInfo
      WHERE UserID = @UserID
    `)

  return {
    userId,
    username,
    nickname: account.recordset[0]?.NickName ?? username,
    email: account.recordset[0]?.Email ?? null,
  }
}

/** Compatibility adapter for the legacy SP_Account_Register flow. */
export async function registerLegacyAccount(
  input: RegistrationInput,
): Promise<{ ok: true } | { ok: false; reason: RegistrationFailure }> {
  const pool = await getSqlPool()

  try {
    const result = await pool.request()
      .input('UserName', sql.VarChar(64), input.username)
      .input('Password', sql.VarChar(256), input.password)
      .input('NickName', sql.VarChar(64), input.nickname)
      .input('Sex', sql.Bit, input.sex)
      .input('Money', sql.Int, 100)
      .input('GiftToken', sql.Int, 100)
      .input('Gold', sql.Int, 100)
      .query<{ Result: number }>(`
        DECLARE @Result INT;

        EXEC @Result = SP_Account_Register
          @UserName,
          @Password,
          @NickName,
          @Sex,
          @Money,
          @GiftToken,
          @Gold;

        SELECT @Result AS Result;
      `)

    const code = Number(result.recordset[0]?.Result)
    if (code === 0) return { ok: true }
    return { ok: false, reason: code === 2 ? 'USERNAME_EXISTS' : 'REGISTER_FAILED' }
  } catch {
    return { ok: false, reason: 'REGISTER_FAILED' }
  }
}
