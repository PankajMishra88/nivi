import { useEffect, useMemo, useState } from 'react'
import {
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { apiRequest } from './api/client'

type UserSummary = {
  id: string
  email: string
  displayName: string
}

type LoginResponse = {
  accessToken: string
  expiresAtUtc: string
  user: UserSummary
}

type Group = {
  id: string
  name: string
}

type Entity = {
  id: string
  groupId: string
  name: string
  type: 'FamilyGroup' | 'BusinessEntity'
}

type Account = {
  id: string
  entityId: string
  name: string
  type: 'Asset' | 'Liability' | 'CreditCard'
  currency: string
  openingBalance: number
  billingCycleDay?: number
  dueDay?: number
}

type Transaction = {
  id: string
  entityId: string
  type:
    | 'Expense'
    | 'Income'
    | 'Transfer'
    | 'Lending'
    | 'Repayment'
    | 'InterestPosting'
    | 'Adjustment'
  status: 'Posted' | 'Pending'
  date: string
  fromAccountId?: string | null
  toAccountId?: string | null
  originalCurrency: string
  originalAmount: number
  baseAmount: number
  fxRateUsed?: number | null
}

const defaultLoginState = {
  tenantSlug: '',
  email: '',
  password: '',
  totpCode: '',
  recoveryCode: '',
}

function App() {
  const [token, setToken] = useState<string | null>(
    localStorage.getItem('nivi_token'),
  )
  const [user, setUser] = useState<UserSummary | null>(null)
  const [loginForm, setLoginForm] = useState(defaultLoginState)
  const [loginError, setLoginError] = useState<string | null>(null)

  const [groups, setGroups] = useState<Group[]>([])
  const [entities, setEntities] = useState<Entity[]>([])
  const [accounts, setAccounts] = useState<Account[]>([])
  const [transactions, setTransactions] = useState<Transaction[]>([])

  const [newGroupName, setNewGroupName] = useState('')
  const [entityForm, setEntityForm] = useState({
    groupId: '',
    name: '',
    type: 'FamilyGroup' as Entity['type'],
  })
  const [accountForm, setAccountForm] = useState({
    entityId: '',
    name: '',
    type: 'Asset' as Account['type'],
    currency: 'INR',
    openingBalance: '',
    billingCycleDay: '',
    dueDay: '',
  })
  const [transactionForm, setTransactionForm] = useState({
    entityId: '',
    type: 'Expense' as Transaction['type'],
    status: 'Posted' as Transaction['status'],
    date: new Date().toISOString().slice(0, 10),
    fromAccountId: '',
    toAccountId: '',
    originalCurrency: 'INR',
    originalAmount: '',
    fxRateUsed: '',
    adjustmentReason: '',
  })
  const [selectedEntityId, setSelectedEntityId] = useState<string>('')
  const [statusMessage, setStatusMessage] = useState<string | null>(null)

  useEffect(() => {
    if (!token) {
      return
    }

    void Promise.all([
      apiRequest<Group[]>('/api/groups', {}, token),
      apiRequest<Entity[]>('/api/entities', {}, token),
      apiRequest<Account[]>('/api/accounts', {}, token),
    ])
      .then(([groupsResponse, entitiesResponse, accountsResponse]) => {
        setGroups(groupsResponse)
        setEntities(entitiesResponse)
        setAccounts(accountsResponse)
        if (!selectedEntityId && entitiesResponse.length > 0) {
          setSelectedEntityId(entitiesResponse[0].id)
        }
      })
      .catch((error: Error) => setStatusMessage(error.message))
  }, [token, selectedEntityId])

  useEffect(() => {
    if (!token || !selectedEntityId) {
      return
    }

    apiRequest<Transaction[]>(
      `/api/transactions?entityId=${selectedEntityId}`,
      {},
      token,
    )
      .then(setTransactions)
      .catch((error: Error) => setStatusMessage(error.message))
  }, [token, selectedEntityId])

  const chartData = useMemo(() => {
    const totals = new Map<string, number>()
    transactions.forEach((tx) => {
      totals.set(tx.date, (totals.get(tx.date) ?? 0) + tx.baseAmount)
    })
    return Array.from(totals.entries()).map(([date, total]) => ({
      date,
      total,
    }))
  }, [transactions])

  const handleLogin = async () => {
    setLoginError(null)
    try {
      const response = await apiRequest<LoginResponse>(
        '/api/auth/login',
        {
          method: 'POST',
          body: JSON.stringify(loginForm),
        },
        undefined,
      )
      setToken(response.accessToken)
      setUser(response.user)
      localStorage.setItem('nivi_token', response.accessToken)
      setLoginForm(defaultLoginState)
    } catch (error) {
      setLoginError(error instanceof Error ? error.message : 'Login failed.')
    }
  }

  const handleLogout = () => {
    setToken(null)
    setUser(null)
    localStorage.removeItem('nivi_token')
  }

  const handleCreateGroup = async () => {
    if (!newGroupName) return
    const created = await apiRequest<Group>(
      '/api/groups',
      {
        method: 'POST',
        body: JSON.stringify({ name: newGroupName }),
      },
      token ?? undefined,
    )
    setGroups((prev) => [...prev, created])
    setNewGroupName('')
  }

  const handleCreateEntity = async () => {
    const created = await apiRequest<Entity>(
      '/api/entities',
      {
        method: 'POST',
        body: JSON.stringify(entityForm),
      },
      token ?? undefined,
    )
    setEntities((prev) => [...prev, created])
    setEntityForm((prev) => ({ ...prev, name: '' }))
  }

  const handleCreateAccount = async () => {
    const payload = {
      ...accountForm,
      openingBalance: Number(accountForm.openingBalance || 0),
      billingCycleDay: accountForm.billingCycleDay
        ? Number(accountForm.billingCycleDay)
        : null,
      dueDay: accountForm.dueDay ? Number(accountForm.dueDay) : null,
    }
    const created = await apiRequest<Account>(
      '/api/accounts',
      {
        method: 'POST',
        body: JSON.stringify(payload),
      },
      token ?? undefined,
    )
    setAccounts((prev) => [...prev, created])
    setAccountForm((prev) => ({ ...prev, name: '', openingBalance: '' }))
  }

  const handleCreateTransaction = async () => {
    const payload = {
      ...transactionForm,
      originalAmount: Number(transactionForm.originalAmount || 0),
      fxRateUsed: transactionForm.fxRateUsed
        ? Number(transactionForm.fxRateUsed)
        : null,
      fromAccountId: transactionForm.fromAccountId || null,
      toAccountId: transactionForm.toAccountId || null,
      adjustmentReason: transactionForm.adjustmentReason || null,
    }
    const created = await apiRequest<Transaction>(
      '/api/transactions',
      {
        method: 'POST',
        body: JSON.stringify(payload),
      },
      token ?? undefined,
    )
    setTransactions((prev) => [created, ...prev])
    setTransactionForm((prev) => ({ ...prev, originalAmount: '', fxRateUsed: '' }))
  }

  if (!token) {
    return (
      <main className="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center px-6">
        <div className="w-full max-w-md space-y-6 rounded-2xl border border-slate-800 bg-slate-900/80 p-8 shadow-xl">
          <div>
            <h1 className="text-2xl font-semibold">Nivi Wealth OS</h1>
            <p className="text-sm text-slate-400">
              Sign in with MFA to continue.
            </p>
          </div>
          {loginError ? (
            <div className="rounded-md border border-red-500/40 bg-red-500/10 px-4 py-2 text-sm text-red-200">
              {loginError}
            </div>
          ) : null}
          <div className="space-y-3">
            <input
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm"
              placeholder="Tenant slug"
              value={loginForm.tenantSlug}
              onChange={(event) =>
                setLoginForm((prev) => ({ ...prev, tenantSlug: event.target.value }))
              }
            />
            <input
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm"
              placeholder="Email"
              type="email"
              value={loginForm.email}
              onChange={(event) =>
                setLoginForm((prev) => ({ ...prev, email: event.target.value }))
              }
            />
            <input
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm"
              placeholder="Password"
              type="password"
              value={loginForm.password}
              onChange={(event) =>
                setLoginForm((prev) => ({ ...prev, password: event.target.value }))
              }
            />
            <input
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm"
              placeholder="TOTP code"
              value={loginForm.totpCode}
              onChange={(event) =>
                setLoginForm((prev) => ({ ...prev, totpCode: event.target.value }))
              }
            />
            <input
              className="w-full rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm"
              placeholder="Recovery code (optional)"
              value={loginForm.recoveryCode}
              onChange={(event) =>
                setLoginForm((prev) => ({
                  ...prev,
                  recoveryCode: event.target.value,
                }))
              }
            />
            <button
              className="w-full rounded-lg bg-emerald-500 px-4 py-2 text-sm font-semibold text-slate-950"
              onClick={handleLogin}
            >
              Sign in
            </button>
          </div>
        </div>
      </main>
    )
  }

  return (
    <main className="min-h-screen bg-slate-50">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <div>
            <h1 className="text-xl font-semibold text-slate-900">
              Nivi Wealth OS
            </h1>
            <p className="text-sm text-slate-500">
              Unified ledger view for tenant operations.
            </p>
          </div>
          <div className="flex items-center gap-4 text-sm text-slate-600">
            {user?.email}
            <button
              className="rounded-lg border border-slate-300 px-3 py-1 text-xs font-semibold uppercase tracking-wide text-slate-700"
              onClick={handleLogout}
            >
              Log out
            </button>
          </div>
        </div>
      </header>

      <div className="mx-auto grid max-w-6xl gap-6 px-6 py-8 lg:grid-cols-[1.2fr_0.8fr]">
        <section className="space-y-6">
          {statusMessage ? (
            <div className="rounded-lg border border-slate-200 bg-white px-4 py-3 text-sm text-slate-600">
              {statusMessage}
            </div>
          ) : null}

          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">
              Transaction Activity
            </h2>
            <p className="text-sm text-slate-500">
              Base currency totals by transaction date.
            </p>
            <div className="mt-4 h-56">
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={chartData}>
                  <XAxis dataKey="date" />
                  <YAxis />
                  <Tooltip />
                  <Line
                    type="monotone"
                    dataKey="total"
                    stroke="#0f766e"
                    strokeWidth={2}
                    dot={false}
                  />
                </LineChart>
              </ResponsiveContainer>
            </div>
          </div>

          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">Create Group</h2>
            <div className="mt-4 flex gap-3">
              <input
                className="flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Group name"
                value={newGroupName}
                onChange={(event) => setNewGroupName(event.target.value)}
              />
              <button
                className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-semibold text-white"
                onClick={handleCreateGroup}
              >
                Add
              </button>
            </div>
          </div>

          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">
              Create Entity
            </h2>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <select
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                value={entityForm.groupId}
                onChange={(event) =>
                  setEntityForm((prev) => ({
                    ...prev,
                    groupId: event.target.value,
                  }))
                }
              >
                <option value="">Select group</option>
                {groups.map((group) => (
                  <option key={group.id} value={group.id}>
                    {group.name}
                  </option>
                ))}
              </select>
              <select
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                value={entityForm.type}
                onChange={(event) =>
                  setEntityForm((prev) => ({
                    ...prev,
                    type: event.target.value as Entity['type'],
                  }))
                }
              >
                <option value="FamilyGroup">Family group</option>
                <option value="BusinessEntity">Business entity</option>
              </select>
              <input
                className="col-span-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Entity name"
                value={entityForm.name}
                onChange={(event) =>
                  setEntityForm((prev) => ({ ...prev, name: event.target.value }))
                }
              />
              <button
                className="col-span-full rounded-lg bg-slate-900 px-4 py-2 text-sm font-semibold text-white"
                onClick={handleCreateEntity}
              >
                Create entity
              </button>
            </div>
          </div>

          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">
              Create Account
            </h2>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <select
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                value={accountForm.entityId}
                onChange={(event) =>
                  setAccountForm((prev) => ({
                    ...prev,
                    entityId: event.target.value,
                  }))
                }
              >
                <option value="">Select entity</option>
                {entities.map((entity) => (
                  <option key={entity.id} value={entity.id}>
                    {entity.name}
                  </option>
                ))}
              </select>
              <select
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                value={accountForm.type}
                onChange={(event) =>
                  setAccountForm((prev) => ({
                    ...prev,
                    type: event.target.value as Account['type'],
                  }))
                }
              >
                <option value="Asset">Asset</option>
                <option value="Liability">Liability</option>
                <option value="CreditCard">Credit card</option>
              </select>
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Account name"
                value={accountForm.name}
                onChange={(event) =>
                  setAccountForm((prev) => ({ ...prev, name: event.target.value }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Currency"
                value={accountForm.currency}
                onChange={(event) =>
                  setAccountForm((prev) => ({
                    ...prev,
                    currency: event.target.value,
                  }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Opening balance"
                value={accountForm.openingBalance}
                onChange={(event) =>
                  setAccountForm((prev) => ({
                    ...prev,
                    openingBalance: event.target.value,
                  }))
                }
              />
              <div className="grid gap-3 sm:grid-cols-2">
                <input
                  className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                  placeholder="Billing cycle day"
                  value={accountForm.billingCycleDay}
                  onChange={(event) =>
                    setAccountForm((prev) => ({
                      ...prev,
                      billingCycleDay: event.target.value,
                    }))
                  }
                />
                <input
                  className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                  placeholder="Due day"
                  value={accountForm.dueDay}
                  onChange={(event) =>
                    setAccountForm((prev) => ({
                      ...prev,
                      dueDay: event.target.value,
                    }))
                  }
                />
              </div>
              <button
                className="col-span-full rounded-lg bg-slate-900 px-4 py-2 text-sm font-semibold text-white"
                onClick={handleCreateAccount}
              >
                Add account
              </button>
            </div>
          </div>

          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">
              Create Transaction
            </h2>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <select
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                value={transactionForm.entityId}
                onChange={(event) =>
                  setTransactionForm((prev) => ({
                    ...prev,
                    entityId: event.target.value,
                  }))
                }
              >
                <option value="">Select entity</option>
                {entities.map((entity) => (
                  <option key={entity.id} value={entity.id}>
                    {entity.name}
                  </option>
                ))}
              </select>
              <select
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                value={transactionForm.type}
                onChange={(event) =>
                  setTransactionForm((prev) => ({
                    ...prev,
                    type: event.target.value as Transaction['type'],
                  }))
                }
              >
                <option value="Expense">Expense</option>
                <option value="Income">Income</option>
                <option value="Transfer">Transfer</option>
                <option value="Lending">Lending</option>
                <option value="Repayment">Repayment</option>
                <option value="InterestPosting">Interest posting</option>
                <option value="Adjustment">Adjustment</option>
              </select>
              <select
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                value={transactionForm.status}
                onChange={(event) =>
                  setTransactionForm((prev) => ({
                    ...prev,
                    status: event.target.value as Transaction['status'],
                  }))
                }
              >
                <option value="Posted">Posted</option>
                <option value="Pending">Pending</option>
              </select>
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                type="date"
                value={transactionForm.date}
                onChange={(event) =>
                  setTransactionForm((prev) => ({
                    ...prev,
                    date: event.target.value,
                  }))
                }
              />
              <select
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                value={transactionForm.fromAccountId}
                onChange={(event) =>
                  setTransactionForm((prev) => ({
                    ...prev,
                    fromAccountId: event.target.value,
                  }))
                }
              >
                <option value="">From account</option>
                {accounts.map((account) => (
                  <option key={account.id} value={account.id}>
                    {account.name}
                  </option>
                ))}
              </select>
              <select
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                value={transactionForm.toAccountId}
                onChange={(event) =>
                  setTransactionForm((prev) => ({
                    ...prev,
                    toAccountId: event.target.value,
                  }))
                }
              >
                <option value="">To account</option>
                {accounts.map((account) => (
                  <option key={account.id} value={account.id}>
                    {account.name}
                  </option>
                ))}
              </select>
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Original currency"
                value={transactionForm.originalCurrency}
                onChange={(event) =>
                  setTransactionForm((prev) => ({
                    ...prev,
                    originalCurrency: event.target.value,
                  }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Original amount"
                value={transactionForm.originalAmount}
                onChange={(event) =>
                  setTransactionForm((prev) => ({
                    ...prev,
                    originalAmount: event.target.value,
                  }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="FX rate (if needed)"
                value={transactionForm.fxRateUsed}
                onChange={(event) =>
                  setTransactionForm((prev) => ({
                    ...prev,
                    fxRateUsed: event.target.value,
                  }))
                }
              />
              <input
                className="col-span-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Adjustment reason (required for adjustments)"
                value={transactionForm.adjustmentReason}
                onChange={(event) =>
                  setTransactionForm((prev) => ({
                    ...prev,
                    adjustmentReason: event.target.value,
                  }))
                }
              />
              <button
                className="col-span-full rounded-lg bg-emerald-500 px-4 py-2 text-sm font-semibold text-white"
                onClick={handleCreateTransaction}
              >
                Record transaction
              </button>
            </div>
          </div>
        </section>

        <aside className="space-y-6">
          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">
              Entities
            </h2>
            <select
              className="mt-3 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
              value={selectedEntityId}
              onChange={(event) => setSelectedEntityId(event.target.value)}
            >
              {entities.map((entity) => (
                <option key={entity.id} value={entity.id}>
                  {entity.name}
                </option>
              ))}
            </select>
            <ul className="mt-4 space-y-2 text-sm text-slate-600">
              {entities.map((entity) => (
                <li
                  key={entity.id}
                  className="rounded-lg border border-slate-200 px-3 py-2"
                >
                  {entity.name} · {entity.type}
                </li>
              ))}
            </ul>
          </div>

          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">
              Accounts
            </h2>
            <ul className="mt-4 space-y-2 text-sm text-slate-600">
              {accounts.map((account) => (
                <li
                  key={account.id}
                  className="rounded-lg border border-slate-200 px-3 py-2"
                >
                  {account.name} · {account.currency} · {account.type}
                </li>
              ))}
            </ul>
          </div>

          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">
              Recent Transactions
            </h2>
            <ul className="mt-4 space-y-2 text-sm text-slate-600">
              {transactions.slice(0, 8).map((tx) => (
                <li
                  key={tx.id}
                  className="rounded-lg border border-slate-200 px-3 py-2"
                >
                  {tx.date} · {tx.type} · {tx.originalCurrency} {tx.originalAmount}
                </li>
              ))}
            </ul>
          </div>
        </aside>
      </div>
    </main>
  )
}

export default App
