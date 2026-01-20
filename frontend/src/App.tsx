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
  openingBalanceBaseAmount: number
  openingBalanceFxRateUsed?: number | null
  billingCycleDay?: number
  dueDay?: number
  billedAmount: number
  unbilledAmount: number
  lastStatementDate?: string | null
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

type AccountBalance = {
  id: string
  entityId: string
  name: string
  type: Account['type']
  currency: string
  openingBalance: number
  openingBalanceBaseAmount: number
  postedDeltaBaseAmount: number
  currentBalanceBaseAmount: number
  billedAmount: number
  unbilledAmount: number
}

type FxRate = {
  id: string
  date: string
  baseCurrency: string
  quoteCurrency: string
  rate: number
}

type Notification = {
  id: string
  title: string
  message: string
  sourceType:
    | 'EmiDue'
    | 'CreditCardBillDue'
    | 'InsuranceRenewal'
    | 'SubscriptionUpcoming'
    | 'TaxDeadline'
    | 'LendingFollowUp'
    | 'GoalCheckIn'
    | 'MissingValuation'
  sourceId?: string | null
  availableAtUtc: string
  readAtUtc?: string | null
}

type Loan = {
  id: string
  entityId: string
  direction: 'Given' | 'Taken'
  name: string
  lenderUserId: string
  borrowerName?: string | null
  originalCurrency: string
  originalAmount: number
  fxRateUsed?: number | null
  baseAmount: number
  nextDueDate?: string | null
  emiAmount?: number | null
  isActive: boolean
}

type InsurancePolicy = {
  id: string
  entityId: string
  providerName: string
  policyNumber: string
  originalCurrency: string
  originalPremium: number
  fxRateUsed?: number | null
  basePremium: number
  renewalDate: string
}

type Subscription = {
  id: string
  entityId: string
  name: string
  originalCurrency: string
  originalAmount: number
  fxRateUsed?: number | null
  baseAmount: number
  nextDueDate: string
  frequency: string
  isActive: boolean
}

type TaxDeadline = {
  id: string
  entityId: string
  name: string
  dueDate: string
  description?: string | null
}

type Goal = {
  id: string
  entityId: string
  name: string
  originalCurrency: string
  originalTargetAmount: number
  fxRateUsed?: number | null
  baseTargetAmount: number
  checkInDate: string
  status: 'Active' | 'Completed' | 'Paused'
}

const defaultLoginState = {
  tenantSlug: '',
  email: '',
  password: '',
  totpCode: '',
  recoveryCode: '',
}

const decodeUserId = (jwt: string | null) => {
  if (!jwt) return null
  try {
    const payload = jwt.split('.')[1]
    const normalized = payload.replace(/-/g, '+').replace(/_/g, '/')
    const decoded = JSON.parse(atob(normalized))
    return decoded.user_id as string | undefined
  } catch {
    return null
  }
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
  const [accountBalances, setAccountBalances] = useState<AccountBalance[]>([])
  const [transactions, setTransactions] = useState<Transaction[]>([])
  const [fxRates, setFxRates] = useState<FxRate[]>([])
  const [notifications, setNotifications] = useState<Notification[]>([])
  const [loans, setLoans] = useState<Loan[]>([])
  const [policies, setPolicies] = useState<InsurancePolicy[]>([])
  const [subscriptions, setSubscriptions] = useState<Subscription[]>([])
  const [taxDeadlines, setTaxDeadlines] = useState<TaxDeadline[]>([])
  const [goals, setGoals] = useState<Goal[]>([])

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
    openingBalanceFxRateUsed: '',
    billingCycleDay: '',
    dueDay: '',
  })
  const [fxRateForm, setFxRateForm] = useState({
    date: new Date().toISOString().slice(0, 10),
    baseCurrency: 'INR',
    quoteCurrency: '',
    rate: '',
  })
  const [loanForm, setLoanForm] = useState({
    direction: 'Given' as Loan['direction'],
    name: '',
    borrowerName: '',
    originalCurrency: 'INR',
    originalAmount: '',
    fxRateUsed: '',
    nextDueDate: new Date().toISOString().slice(0, 10),
    emiAmount: '',
  })
  const [policyForm, setPolicyForm] = useState({
    providerName: '',
    policyNumber: '',
    originalCurrency: 'INR',
    originalPremium: '',
    fxRateUsed: '',
    renewalDate: new Date().toISOString().slice(0, 10),
  })
  const [subscriptionForm, setSubscriptionForm] = useState({
    name: '',
    originalCurrency: 'INR',
    originalAmount: '',
    fxRateUsed: '',
    nextDueDate: new Date().toISOString().slice(0, 10),
    frequency: 'Monthly',
  })
  const [taxDeadlineForm, setTaxDeadlineForm] = useState({
    name: '',
    dueDate: new Date().toISOString().slice(0, 10),
    description: '',
  })
  const [goalForm, setGoalForm] = useState({
    name: '',
    originalCurrency: 'INR',
    originalTargetAmount: '',
    fxRateUsed: '',
    checkInDate: new Date().toISOString().slice(0, 10),
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
      .then(() =>
        apiRequest<FxRate[]>('/api/fx-rates', {}, token)
          .then(setFxRates)
          .catch(() => undefined),
      )
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

  useEffect(() => {
    if (!token || !selectedEntityId) {
      return
    }

    apiRequest<AccountBalance[]>(
      `/api/accounts/balances?entityId=${selectedEntityId}`,
      {},
      token,
    )
      .then(setAccountBalances)
      .catch((error: Error) => setStatusMessage(error.message))
  }, [token, selectedEntityId])

  useEffect(() => {
    if (!token) {
      return
    }

    apiRequest<Notification[]>('/api/notifications', {}, token)
      .then(setNotifications)
      .catch(() => undefined)
  }, [token])

  useEffect(() => {
    if (!token || !selectedEntityId) {
      return
    }

    void Promise.all([
      apiRequest<Loan[]>(`/api/loans?entityId=${selectedEntityId}`, {}, token),
      apiRequest<InsurancePolicy[]>(
        `/api/insurance-policies?entityId=${selectedEntityId}`,
        {},
        token,
      ),
      apiRequest<Subscription[]>(
        `/api/subscriptions?entityId=${selectedEntityId}`,
        {},
        token,
      ),
      apiRequest<TaxDeadline[]>(
        `/api/tax-deadlines?entityId=${selectedEntityId}`,
        {},
        token,
      ),
      apiRequest<Goal[]>(`/api/goals?entityId=${selectedEntityId}`, {}, token),
    ])
      .then(([loanResponse, policyResponse, subscriptionResponse, deadlineResponse, goalResponse]) => {
        setLoans(loanResponse)
        setPolicies(policyResponse)
        setSubscriptions(subscriptionResponse)
        setTaxDeadlines(deadlineResponse)
        setGoals(goalResponse)
      })
      .catch(() => undefined)
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

  const balanceLookup = useMemo(() => {
    return new Map(accountBalances.map((balance) => [balance.id, balance]))
  }, [accountBalances])

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

  const ensureEntitySelected = () => {
    if (!selectedEntityId) {
      setStatusMessage('Select an entity first.')
      return false
    }
    return true
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
      openingBalanceFxRateUsed: accountForm.openingBalanceFxRateUsed
        ? Number(accountForm.openingBalanceFxRateUsed)
        : null,
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
    setAccountForm((prev) => ({
      ...prev,
      name: '',
      openingBalance: '',
      openingBalanceFxRateUsed: '',
    }))
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
    if (selectedEntityId) {
      apiRequest<AccountBalance[]>(
        `/api/accounts/balances?entityId=${selectedEntityId}`,
        {},
        token ?? undefined,
      )
        .then(setAccountBalances)
        .catch(() => undefined)
    }
  }

  const handleCreateFxRate = async () => {
    const payload = {
      ...fxRateForm,
      rate: Number(fxRateForm.rate || 0),
    }
    const created = await apiRequest<FxRate>(
      '/api/fx-rates',
      {
        method: 'POST',
        body: JSON.stringify(payload),
      },
      token ?? undefined,
    )
    setFxRates((prev) => [created, ...prev])
    setFxRateForm((prev) => ({ ...prev, quoteCurrency: '', rate: '' }))
  }

  const refreshNotifications = async () => {
    if (!token) return
    const response = await apiRequest<Notification[]>('/api/notifications', {}, token)
    setNotifications(response)
  }

  const handleMarkNotificationRead = async (notificationId: string) => {
    if (!token) return
    await apiRequest<void>(
      `/api/notifications/${notificationId}/read`,
      { method: 'POST' },
      token,
    )
    setNotifications((prev) => prev.filter((item) => item.id !== notificationId))
  }

  const handleCreateLoan = async () => {
    if (!token || !ensureEntitySelected()) return
    const lenderUserId = user?.id ?? decodeUserId(token)
    if (!lenderUserId) {
      setStatusMessage('Missing lender user context.')
      return
    }
    const payload = {
      entityId: selectedEntityId,
      direction: loanForm.direction,
      name: loanForm.name,
      lenderUserId,
      borrowerName: loanForm.borrowerName || null,
      originalCurrency: loanForm.originalCurrency,
      originalAmount: Number(loanForm.originalAmount || 0),
      fxRateUsed: loanForm.fxRateUsed ? Number(loanForm.fxRateUsed) : null,
      nextDueDate: loanForm.nextDueDate || null,
      emiAmount: loanForm.emiAmount ? Number(loanForm.emiAmount) : null,
    }
    const created = await apiRequest<Loan>(
      '/api/loans',
      {
        method: 'POST',
        body: JSON.stringify(payload),
      },
      token,
    )
    setLoans((prev) => [created, ...prev])
    setLoanForm((prev) => ({ ...prev, name: '', borrowerName: '', originalAmount: '', fxRateUsed: '', emiAmount: '' }))
  }

  const handleCreatePolicy = async () => {
    if (!token || !ensureEntitySelected()) return
    const payload = {
      entityId: selectedEntityId,
      providerName: policyForm.providerName,
      policyNumber: policyForm.policyNumber,
      originalCurrency: policyForm.originalCurrency,
      originalPremium: Number(policyForm.originalPremium || 0),
      fxRateUsed: policyForm.fxRateUsed ? Number(policyForm.fxRateUsed) : null,
      renewalDate: policyForm.renewalDate,
    }
    const created = await apiRequest<InsurancePolicy>(
      '/api/insurance-policies',
      {
        method: 'POST',
        body: JSON.stringify(payload),
      },
      token,
    )
    setPolicies((prev) => [created, ...prev])
    setPolicyForm((prev) => ({ ...prev, providerName: '', policyNumber: '', originalPremium: '', fxRateUsed: '' }))
  }

  const handleCreateSubscription = async () => {
    if (!token || !ensureEntitySelected()) return
    const payload = {
      entityId: selectedEntityId,
      name: subscriptionForm.name,
      originalCurrency: subscriptionForm.originalCurrency,
      originalAmount: Number(subscriptionForm.originalAmount || 0),
      fxRateUsed: subscriptionForm.fxRateUsed ? Number(subscriptionForm.fxRateUsed) : null,
      nextDueDate: subscriptionForm.nextDueDate,
      frequency: subscriptionForm.frequency,
    }
    const created = await apiRequest<Subscription>(
      '/api/subscriptions',
      {
        method: 'POST',
        body: JSON.stringify(payload),
      },
      token,
    )
    setSubscriptions((prev) => [created, ...prev])
    setSubscriptionForm((prev) => ({ ...prev, name: '', originalAmount: '', fxRateUsed: '' }))
  }

  const handleCreateTaxDeadline = async () => {
    if (!token || !ensureEntitySelected()) return
    const payload = {
      entityId: selectedEntityId,
      name: taxDeadlineForm.name,
      dueDate: taxDeadlineForm.dueDate,
      description: taxDeadlineForm.description || null,
    }
    const created = await apiRequest<TaxDeadline>(
      '/api/tax-deadlines',
      {
        method: 'POST',
        body: JSON.stringify(payload),
      },
      token,
    )
    setTaxDeadlines((prev) => [created, ...prev])
    setTaxDeadlineForm((prev) => ({ ...prev, name: '', description: '' }))
  }

  const handleCreateGoal = async () => {
    if (!token || !ensureEntitySelected()) return
    const payload = {
      entityId: selectedEntityId,
      name: goalForm.name,
      originalCurrency: goalForm.originalCurrency,
      originalTargetAmount: Number(goalForm.originalTargetAmount || 0),
      fxRateUsed: goalForm.fxRateUsed ? Number(goalForm.fxRateUsed) : null,
      checkInDate: goalForm.checkInDate,
    }
    const created = await apiRequest<Goal>(
      '/api/goals',
      {
        method: 'POST',
        body: JSON.stringify(payload),
      },
      token,
    )
    setGoals((prev) => [created, ...prev])
    setGoalForm((prev) => ({ ...prev, name: '', originalTargetAmount: '', fxRateUsed: '' }))
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
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Opening balance FX rate (if non-base)"
                value={accountForm.openingBalanceFxRateUsed}
                onChange={(event) =>
                  setAccountForm((prev) => ({
                    ...prev,
                    openingBalanceFxRateUsed: event.target.value,
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

          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">Create Loan</h2>
            <p className="text-sm text-slate-500">
              Uses the currently selected entity for scope.
            </p>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <select
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                value={loanForm.direction}
                onChange={(event) =>
                  setLoanForm((prev) => ({
                    ...prev,
                    direction: event.target.value as Loan['direction'],
                  }))
                }
              >
                <option value="Given">Given</option>
                <option value="Taken">Taken</option>
              </select>
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Loan name"
                value={loanForm.name}
                onChange={(event) =>
                  setLoanForm((prev) => ({ ...prev, name: event.target.value }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Borrower name"
                value={loanForm.borrowerName}
                onChange={(event) =>
                  setLoanForm((prev) => ({
                    ...prev,
                    borrowerName: event.target.value,
                  }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Currency"
                value={loanForm.originalCurrency}
                onChange={(event) =>
                  setLoanForm((prev) => ({
                    ...prev,
                    originalCurrency: event.target.value,
                  }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Principal amount"
                value={loanForm.originalAmount}
                onChange={(event) =>
                  setLoanForm((prev) => ({
                    ...prev,
                    originalAmount: event.target.value,
                  }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="FX rate (if needed)"
                value={loanForm.fxRateUsed}
                onChange={(event) =>
                  setLoanForm((prev) => ({ ...prev, fxRateUsed: event.target.value }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                type="date"
                value={loanForm.nextDueDate}
                onChange={(event) =>
                  setLoanForm((prev) => ({ ...prev, nextDueDate: event.target.value }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="EMI amount (optional)"
                value={loanForm.emiAmount}
                onChange={(event) =>
                  setLoanForm((prev) => ({ ...prev, emiAmount: event.target.value }))
                }
              />
              <button
                className="col-span-full rounded-lg bg-slate-900 px-4 py-2 text-sm font-semibold text-white"
                onClick={handleCreateLoan}
              >
                Add loan
              </button>
            </div>
            <ul className="mt-4 space-y-2 text-xs text-slate-600">
              {loans.slice(0, 3).map((loan) => (
                <li key={loan.id} className="rounded-lg border border-slate-200 px-3 py-2">
                  {loan.name} · {loan.direction} · {loan.originalCurrency}{' '}
                  {loan.originalAmount}
                </li>
              ))}
            </ul>
          </div>

          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">
              Create Insurance Policy
            </h2>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Provider"
                value={policyForm.providerName}
                onChange={(event) =>
                  setPolicyForm((prev) => ({
                    ...prev,
                    providerName: event.target.value,
                  }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Policy number"
                value={policyForm.policyNumber}
                onChange={(event) =>
                  setPolicyForm((prev) => ({
                    ...prev,
                    policyNumber: event.target.value,
                  }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Currency"
                value={policyForm.originalCurrency}
                onChange={(event) =>
                  setPolicyForm((prev) => ({
                    ...prev,
                    originalCurrency: event.target.value,
                  }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Premium amount"
                value={policyForm.originalPremium}
                onChange={(event) =>
                  setPolicyForm((prev) => ({
                    ...prev,
                    originalPremium: event.target.value,
                  }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="FX rate (if needed)"
                value={policyForm.fxRateUsed}
                onChange={(event) =>
                  setPolicyForm((prev) => ({ ...prev, fxRateUsed: event.target.value }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                type="date"
                value={policyForm.renewalDate}
                onChange={(event) =>
                  setPolicyForm((prev) => ({ ...prev, renewalDate: event.target.value }))
                }
              />
              <button
                className="col-span-full rounded-lg bg-slate-900 px-4 py-2 text-sm font-semibold text-white"
                onClick={handleCreatePolicy}
              >
                Add policy
              </button>
            </div>
            <ul className="mt-4 space-y-2 text-xs text-slate-600">
              {policies.slice(0, 3).map((policy) => (
                <li key={policy.id} className="rounded-lg border border-slate-200 px-3 py-2">
                  {policy.policyNumber} · {policy.originalCurrency}{' '}
                  {policy.originalPremium}
                </li>
              ))}
            </ul>
          </div>

          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">
              Create Subscription
            </h2>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Subscription name"
                value={subscriptionForm.name}
                onChange={(event) =>
                  setSubscriptionForm((prev) => ({ ...prev, name: event.target.value }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Currency"
                value={subscriptionForm.originalCurrency}
                onChange={(event) =>
                  setSubscriptionForm((prev) => ({
                    ...prev,
                    originalCurrency: event.target.value,
                  }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Amount"
                value={subscriptionForm.originalAmount}
                onChange={(event) =>
                  setSubscriptionForm((prev) => ({
                    ...prev,
                    originalAmount: event.target.value,
                  }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="FX rate (if needed)"
                value={subscriptionForm.fxRateUsed}
                onChange={(event) =>
                  setSubscriptionForm((prev) => ({ ...prev, fxRateUsed: event.target.value }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                type="date"
                value={subscriptionForm.nextDueDate}
                onChange={(event) =>
                  setSubscriptionForm((prev) => ({
                    ...prev,
                    nextDueDate: event.target.value,
                  }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Frequency"
                value={subscriptionForm.frequency}
                onChange={(event) =>
                  setSubscriptionForm((prev) => ({
                    ...prev,
                    frequency: event.target.value,
                  }))
                }
              />
              <button
                className="col-span-full rounded-lg bg-slate-900 px-4 py-2 text-sm font-semibold text-white"
                onClick={handleCreateSubscription}
              >
                Add subscription
              </button>
            </div>
            <ul className="mt-4 space-y-2 text-xs text-slate-600">
              {subscriptions.slice(0, 3).map((subscription) => (
                <li
                  key={subscription.id}
                  className="rounded-lg border border-slate-200 px-3 py-2"
                >
                  {subscription.name} · {subscription.originalCurrency}{' '}
                  {subscription.originalAmount}
                </li>
              ))}
            </ul>
          </div>

          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">
              Create Tax Deadline
            </h2>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Deadline name"
                value={taxDeadlineForm.name}
                onChange={(event) =>
                  setTaxDeadlineForm((prev) => ({ ...prev, name: event.target.value }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                type="date"
                value={taxDeadlineForm.dueDate}
                onChange={(event) =>
                  setTaxDeadlineForm((prev) => ({
                    ...prev,
                    dueDate: event.target.value,
                  }))
                }
              />
              <input
                className="col-span-full rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Description"
                value={taxDeadlineForm.description}
                onChange={(event) =>
                  setTaxDeadlineForm((prev) => ({
                    ...prev,
                    description: event.target.value,
                  }))
                }
              />
              <button
                className="col-span-full rounded-lg bg-slate-900 px-4 py-2 text-sm font-semibold text-white"
                onClick={handleCreateTaxDeadline}
              >
                Add deadline
              </button>
            </div>
            <ul className="mt-4 space-y-2 text-xs text-slate-600">
              {taxDeadlines.slice(0, 3).map((deadline) => (
                <li
                  key={deadline.id}
                  className="rounded-lg border border-slate-200 px-3 py-2"
                >
                  {deadline.name} · {deadline.dueDate}
                </li>
              ))}
            </ul>
          </div>

          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">Create Goal</h2>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Goal name"
                value={goalForm.name}
                onChange={(event) =>
                  setGoalForm((prev) => ({ ...prev, name: event.target.value }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Currency"
                value={goalForm.originalCurrency}
                onChange={(event) =>
                  setGoalForm((prev) => ({
                    ...prev,
                    originalCurrency: event.target.value,
                  }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Target amount"
                value={goalForm.originalTargetAmount}
                onChange={(event) =>
                  setGoalForm((prev) => ({
                    ...prev,
                    originalTargetAmount: event.target.value,
                  }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="FX rate (if needed)"
                value={goalForm.fxRateUsed}
                onChange={(event) =>
                  setGoalForm((prev) => ({ ...prev, fxRateUsed: event.target.value }))
                }
              />
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                type="date"
                value={goalForm.checkInDate}
                onChange={(event) =>
                  setGoalForm((prev) => ({ ...prev, checkInDate: event.target.value }))
                }
              />
              <button
                className="col-span-full rounded-lg bg-emerald-500 px-4 py-2 text-sm font-semibold text-white"
                onClick={handleCreateGoal}
              >
                Add goal
              </button>
            </div>
            <ul className="mt-4 space-y-2 text-xs text-slate-600">
              {goals.slice(0, 3).map((goal) => (
                <li key={goal.id} className="rounded-lg border border-slate-200 px-3 py-2">
                  {goal.name} · {goal.checkInDate}
                </li>
              ))}
            </ul>
          </div>
        </section>

        <aside className="space-y-6">
          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold text-slate-900">
                Notifications
              </h2>
              <button
                className="text-xs font-semibold text-emerald-600"
                onClick={refreshNotifications}
              >
                Refresh
              </button>
            </div>
            <ul className="mt-4 space-y-2 text-xs text-slate-600">
              {notifications.length === 0 ? (
                <li className="rounded-lg border border-slate-200 px-3 py-2">
                  No new reminders.
                </li>
              ) : (
                notifications.slice(0, 6).map((notification) => (
                  <li
                    key={notification.id}
                    className="rounded-lg border border-slate-200 px-3 py-2"
                  >
                    <div className="text-sm font-medium text-slate-700">
                      {notification.title}
                    </div>
                    <div className="text-xs text-slate-500">
                      {notification.message}
                    </div>
                    <button
                      className="mt-2 text-xs font-semibold text-slate-600"
                      onClick={() => handleMarkNotificationRead(notification.id)}
                    >
                      Mark read
                    </button>
                  </li>
                ))
              )}
            </ul>
          </div>
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
                  <div className="font-medium text-slate-700">{account.name}</div>
                  <div className="text-xs text-slate-500">
                    {account.currency} · {account.type}
                  </div>
                  {balanceLookup.has(account.id) ? (
                    <div className="mt-1 text-xs text-slate-500">
                      Balance (base):{' '}
                      {balanceLookup
                        .get(account.id)
                        ?.currentBalanceBaseAmount.toFixed(2)}
                    </div>
                  ) : null}
                  {account.type === 'CreditCard' ? (
                    <div className="mt-1 text-xs text-slate-500">
                      Billed: {account.billedAmount.toFixed(2)} · Unbilled:{' '}
                      {account.unbilledAmount.toFixed(2)}
                    </div>
                  ) : null}
                </li>
              ))}
            </ul>
          </div>

          <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">
              FX Rates
            </h2>
            <div className="mt-4 grid gap-2 text-sm">
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                type="date"
                value={fxRateForm.date}
                onChange={(event) =>
                  setFxRateForm((prev) => ({ ...prev, date: event.target.value }))
                }
              />
              <div className="grid gap-2 sm:grid-cols-2">
                <input
                  className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                  placeholder="Base currency"
                  value={fxRateForm.baseCurrency}
                  onChange={(event) =>
                    setFxRateForm((prev) => ({
                      ...prev,
                      baseCurrency: event.target.value,
                    }))
                  }
                />
                <input
                  className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                  placeholder="Quote currency"
                  value={fxRateForm.quoteCurrency}
                  onChange={(event) =>
                    setFxRateForm((prev) => ({
                      ...prev,
                      quoteCurrency: event.target.value,
                    }))
                  }
                />
              </div>
              <input
                className="rounded-lg border border-slate-300 px-3 py-2 text-sm"
                placeholder="Rate"
                value={fxRateForm.rate}
                onChange={(event) =>
                  setFxRateForm((prev) => ({ ...prev, rate: event.target.value }))
                }
              />
              <button
                className="rounded-lg bg-slate-900 px-3 py-2 text-xs font-semibold text-white"
                onClick={handleCreateFxRate}
              >
                Save FX rate
              </button>
            </div>
            <ul className="mt-4 space-y-2 text-xs text-slate-600">
              {fxRates.slice(0, 5).map((rate) => (
                <li
                  key={rate.id}
                  className="rounded-lg border border-slate-200 px-3 py-2"
                >
                  {rate.date} · {rate.baseCurrency}/{rate.quoteCurrency} ·{' '}
                  {rate.rate}
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
