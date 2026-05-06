import {
  BOARD_SIZE,
  FLEET_ORDER,
  type OrientationName,
  type ShipTypeName,
  isWithinBounds,
  shipCells,
} from "./rules.js";

export type ShotResultType = "Miss" | "Hit" | "Sunk" | "AlreadyShot" | "Invalid";

export interface ShotResult {
  ResultType: ShotResultType;
  Row: number;
  Col: number;
  SunkShipType: ShipTypeName | null;
}

interface Ship {
  Type: ShipTypeName;
  Coordinates: Array<{ Row: number; Col: number }>;
  Hits: Set<string>;
}

function key(r: number, c: number): string {
  return `${r},${c}`;
}

export class Board {
  readonly Ships: Ship[] = [];
  readonly ShotsReceived = new Set<string>();

  get AllShipsPlaced(): boolean {
    return this.Ships.length === FLEET_ORDER.length;
  }

  get AllShipsSunk(): boolean {
    return (
      this.Ships.length > 0 && this.Ships.every((s) => s.Hits.size >= s.Coordinates.length)
    );
  }

  private occupies(row: number, col: number): boolean {
    return this.Ships.some((s) =>
      s.Coordinates.some((c) => c.Row === row && c.Col === col),
    );
  }

  CanPlaceShip(
    type: ShipTypeName,
    row: number,
    col: number,
    orientation: OrientationName,
  ): boolean {
    const coords = shipCells(row, col, type, orientation);
    for (const c of coords) {
      if (!isWithinBounds(c.Row, c.Col) || this.occupies(c.Row, c.Col)) {
        return false;
      }
    }
    return true;
  }

  PlaceShip(
    type: ShipTypeName,
    row: number,
    col: number,
    orientation: OrientationName,
  ): boolean {
    if (!this.CanPlaceShip(type, row, col, orientation)) {
      return false;
    }
    if (this.Ships.some((s) => s.Type === type)) {
      return false;
    }
    const coordinates = shipCells(row, col, type, orientation);
    this.Ships.push({
      Type: type,
      Coordinates: coordinates,
      Hits: new Set(),
    });
    return true;
  }

  FireAt(row: number, col: number): ShotResult {
    if (!isWithinBounds(row, col)) {
      return { ResultType: "Invalid", Row: row, Col: col, SunkShipType: null };
    }
    const k = key(row, col);
    if (this.ShotsReceived.has(k)) {
      return { ResultType: "AlreadyShot", Row: row, Col: col, SunkShipType: null };
    }
    this.ShotsReceived.add(k);

    const hitShip = this.Ships.find((s) =>
      s.Coordinates.some((c) => c.Row === row && c.Col === col),
    );

    if (!hitShip) {
      return { ResultType: "Miss", Row: row, Col: col, SunkShipType: null };
    }
    hitShip.Hits.add(k);
    const sunk = hitShip.Hits.size >= hitShip.Coordinates.length;
    if (sunk) {
      return { ResultType: "Sunk", Row: row, Col: col, SunkShipType: hitShip.Type };
    }
    return { ResultType: "Hit", Row: row, Col: col, SunkShipType: null };
  }
}
