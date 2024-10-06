

export const formatRequestHeaders = (headers: Array<{ key: string; value: string }>): { [key: string]: string } => {
  return headers.reduce((accumulator, curVal) => {
    return { ...accumulator, [curVal.key]: curVal.value }
  }, {})

}