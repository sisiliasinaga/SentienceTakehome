export const BOARD_SIZE = 10;

export type ShipTypeName =
  | "Carrier"
  | "Battleship"
  | "Cruiser"
  | "Submarine"
  | "Destroyer";

export type OrientationName = "Horizontal" | "Vertical";

export const FLEET_ORDER: readonly ShipTypeName[] = [
  "Carrier",
  "Battleship",
  "Cruiser",
  "Submarine",
  "Destroyer",
] as const;

export function shipLength(type: ShipTypeName): number {
  switch (type) {
    case "Carrier":
      return 5;
    case "Battleship":
      return 4;
    case "Cruiser":
    case "Submarine":
      return 3;
    case "Destroyer":
      return 2;
    default: {
      const _exhaustive: never = type;
      return _exhaustive;
    }
  }
}

export function isWithinBounds(row: number, col: number): boolean {
  return row >= 0 && row < BOARD_SIZE && col >= 0 && col < BOARD_SIZE;
}

export function shipCells(
  row: number,
  col: number,
  ship: ShipTypeName,
  orientation: OrientationName,
): Array<{ Row: number; Col: number }> {
  const len = shipLength(ship);
  const out: Array<{ Row: number; Col: number }> = [];
  for (let i = 0; i < len; i++) {
    out.push({
      Row: row + (orientation === "Vertical" ? i : 0),
      Col: col + (orientation === "Horizontal" ? i : 0),
    });
  }
  return out;
}

export function isShipTypeName(s: string): s is ShipTypeName {
  return (FLEET_ORDER as readonly string[]).includes(s);
}

export function isOrientationName(s: string): s is OrientationName {
  return s === "Horizontal" || s === "Vertical";
}
