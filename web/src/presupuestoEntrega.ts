export interface SuggestedDownPaymentDecision {
  suggested: number
  value: number
  apply: boolean
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
