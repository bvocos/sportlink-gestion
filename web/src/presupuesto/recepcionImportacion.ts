export type ReceptionRoll = { posicion: 'A' | 'B' | 'C'; codigoBarra: string; cantidadM2: number }
export type ReceptionLine = { controlPorLotes: boolean; cantidadM2: number; rollos: ReceptionRoll[] }

export function receptionLineTotal(line: ReceptionLine) {
  return line.controlPorLotes
    ? line.rollos.reduce((sum, roll) => sum + Number(roll.cantidadM2 || 0), 0)
    : Number(line.cantidadM2 || 0)
}

export function receptionTotal(lines: ReceptionLine[]) {
  return lines.reduce((sum, line) => sum + receptionLineTotal(line), 0)
}

export function receptionLineErrors(line: ReceptionLine) {
  const errors: string[] = []
  if (!line.controlPorLotes && Number(line.cantidadM2) <= 0) errors.push('Ingresá una cantidad mayor que cero.')
  if (line.controlPorLotes) {
    if (line.rollos.length !== 3 || line.rollos.some(roll => !roll.codigoBarra.trim() || Number(roll.cantidadM2) <= 0))
      errors.push('Completá los tres rollos con código y m².')
    const codes = line.rollos.map(roll => roll.codigoBarra.trim().toLowerCase()).filter(Boolean)
    if (new Set(codes).size !== codes.length) errors.push('Los códigos de barra no pueden repetirse.')
  }
  return errors
}
