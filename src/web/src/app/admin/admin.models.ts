export type CellStatus = 'Free' | 'Partial' | 'Booked' | 'Unavailable';

export interface DashboardCell {
  start: string;
  end: string;
  status: CellStatus;
  freeSeats: number;
}

export interface DashboardRoom {
  spaceId: string;
  name: string;
  building: string;
  type: string;
  mode: 'Dedicated' | 'Mixed';
  capacity: number;
  cells: DashboardCell[];
}

export interface DashboardDto {
  date: string;
  rooms: DashboardRoom[];
}

export interface DashboardParams {
  date: string;
  building?: string;
  type?: string;
}

export const CELL_STATUS_LABELS: Readonly<Record<CellStatus, string>> = {
  Free: 'Libera',
  Partial: 'Posti liberi',
  Booked: 'Prenotata',
  Unavailable: 'Non disponibile',
};
