export const ALL_PERMISSIONS = "*";

/** True when the permission set grants the given `module.action` key (or holds `*`). */
export function hasPermission(granted: readonly string[], required: string): boolean {
  return granted.includes(ALL_PERMISSIONS) || granted.includes(required);
}

/** True when the set grants at least one of the given keys. */
export function hasAnyPermission(granted: readonly string[], required: readonly string[]): boolean {
  return granted.includes(ALL_PERMISSIONS) || required.some((key) => granted.includes(key));
}
