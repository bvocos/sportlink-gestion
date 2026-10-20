export interface SuggestedDownPaymentDecision {
  suggested: number
  value: number
  apply: boolean
}

const SAN_FRANCISCO_BRANCH_ID = '7b100000-0000-0000-0000-000000000001'

export function isDownPaymentSuggestionEnabled(sucursalId: string | null | undefined): boolean {
  return sucursalId?.toLowerCase() === SAN_FRANCISCO_BRANCH_ID
}

export function resolveSuggestedDownPayment(
  currentValue: number,
  serverSuggestion: number,
  financedTotal: number,
  manuallyModified: boolean,
): SuggestedDownPaymentDecision {
  const safeSuggestion = Number.isFinite(serverSuggestion) ? Math.max(0, serverSuggestion) : 0
  const safeTotal = Number.isFinite(financedTotal) ? Math.max(0, financedTotal) : 0
  const suggested = Math.min(safeSuggestion, safeTotal)
  return {
    suggested,
    value: manuallyModified ? currentValue : suggested,
    apply: !manuallyModified,
  }
}
