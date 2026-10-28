export type SpaceType = 'Classroom' | 'Lab' | 'StudyRoom';

export type SpaceMode = 'Dedicated' | 'Mixed';

export interface UnavailabilityDto {
  id: string;
  start: string;
  end: string;
  reason: string;
}

export interface SpaceDto {
  id: string;
  name: string;
  type: SpaceType;
  mode: SpaceMode;
  building: string;
  floor: number;
  capacity: number;
  isActive: boolean;
  features: Record<string, unknown>;
  equipment: string[];
  unavailabilities: UnavailabilityDto[];
  updatedAt: string;
}

export interface SpaceSearchParams {
  search?: string;
  type?: SpaceType | '';
  mode?: SpaceMode | '';
  minCapacity?: number | null;
  building?: string;
  equipment?: string[];
  software?: string;
}

export interface BatchAvailabilityPayload {
  spaceIds: string[];
  start: string;
  end: string;
}

export interface TimeSlotRange {
  date: string;
  startTime: string;
  endTime: string;
  startIso: string;
  endIso: string;
}

export interface AvailabilitySlotDto {
  start: string;
  end: string;
  occupiedSeats: number;
  freeSeats: number;
  isAvailable: boolean;
}

export interface SpaceAvailabilityDto {
  spaceId: string;
  date: string;
  accessMode: SpaceMode;
  capacity: number;
  slots: AvailabilitySlotDto[];
}

export type TimelineSegmentStatus = 'free' | 'partial' | 'occupied' | 'unavailable' | 'inactive';

export interface DailyTimelineSegment {
  startIso: string;
  endIso: string;
  startTimeRome: string;
  endTimeRome: string;
  status: TimelineSegmentStatus;
  occupiedSeats: number;
  freeSeats: number;
  capacity: number;
  reason?: string;
}

export const SPACE_TYPE_OPTIONS: ReadonlyArray<{ value: SpaceType; label: string }> = [
  { value: 'Classroom', label: 'Aula' },
  { value: 'Lab', label: 'Laboratorio' },
  { value: 'StudyRoom', label: 'Sala studio' },
];

export const SPACE_TYPE_LABELS: Readonly<Record<SpaceType, string>> = {
  Classroom: 'Aula',
  Lab: 'Laboratorio',
  StudyRoom: 'Sala studio',
};

export const SPACE_MODE_OPTIONS: ReadonlyArray<{ value: SpaceMode; label: string }> = [
  { value: 'Dedicated', label: 'Uso dedicato' },
  { value: 'Mixed', label: 'Uso misto' },
];

export const SPACE_MODE_LABELS: Readonly<Record<SpaceMode, string>> = {
  Dedicated: 'Uso dedicato',
  Mixed: 'Uso misto',
};

export const SPACE_MODE_HINTS: Readonly<Record<SpaceMode, string>> = {
  Dedicated: 'Prenotazione intera aula per gruppi',
  Mixed: 'Posti singoli o piccoli gruppi fino a esaurimento capienza',
};

export const EQUIPMENT_OPTIONS: ReadonlyArray<{ value: string; label: string }> = [
  { value: 'videoprojector', label: 'Videoproiettore' },
  { value: 'interactive-whiteboard', label: 'Lavagna interattiva' },
  { value: 'audio-kit', label: 'Kit audio' },
  { value: 'microphone', label: 'Microfono' },
];

const EQUIPMENT_LABEL_MAP: Readonly<Record<string, string>> = {
  videoprojector: 'Videoproiettore',
  'interactive-whiteboard': 'Lavagna interattiva',
  'audio-kit': 'Kit audio',
  microphone: 'Microfono',
  'hdmi-adapter': 'Adattatore HDMI',
};

const FEATURE_BOOLEAN_LABELS: Readonly<Record<string, string>> = {
  blackboard: 'Lavagna tradizionale',
  projector: 'Proiettore fisso',
  poweroutlets: 'Prese elettriche ai tavoli',
  wifihighspeed: 'Wi-Fi ad alta velocità',
  quietzone: 'Zona silenzio',
  movabledesks: 'Banchi modulari',
  audiosystem: 'Impianto audio di sala',
  tierseating: 'Posti a gradoni',
  audiokit: 'Postazione audio',
};

export function formatEquipmentLabel(slug: string): string {
  const normalized = slug.trim().toLowerCase();
  if (EQUIPMENT_LABEL_MAP[normalized]) {
    return EQUIPMENT_LABEL_MAP[normalized];
  }

  return slug
    .trim()
    .replace(/[-_]+/g, ' ')
    .replace(/\b\w/g, (letter) => letter.toUpperCase());
}

export function formatFloorLabel(floor: number): string {
  if (floor === 0) {
    return 'Piano terra';
  }
  if (floor < 0) {
    return `Piano ${floor}`;
  }
  return `Piano ${floor}`;
}

export function extractSpaceFeatures(features: Record<string, unknown> | null | undefined): string[] {
  if (!features) {
    return [];
  }

  const badges: string[] = [];

  for (const [rawKey, value] of Object.entries(features)) {
    const key = rawKey.trim().toLowerCase();

    if (key === 'workstations' && typeof value === 'number' && value > 0) {
      badges.push(`${value} postazioni PC`);
      continue;
    }

    if (key === 'software' && Array.isArray(value)) {
      const softwareNames = value
        .filter((item): item is string => typeof item === 'string' && item.trim().length > 0)
        .map((item) => item.trim());
      if (softwareNames.length > 0) {
        badges.push(`Software: ${softwareNames.join(', ')}`);
      }
      continue;
    }

    if (value === true && FEATURE_BOOLEAN_LABELS[key]) {
      badges.push(FEATURE_BOOLEAN_LABELS[key]);
    }
  }

  return badges;
}

const DATE_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/;
const TIME_PATTERN = /^(\d{2}):(\d{2})$/;

const ROME_PARTS_FORMATTER = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Europe/Rome',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hourCycle: 'h23',
});

function getRomeOffsetMs(utcMs: number): number {
  const parts = ROME_PARTS_FORMATTER.formatToParts(new Date(utcMs));
  const values: Record<string, number> = {};
  for (const part of parts) {
    if (part.type !== 'literal') {
      values[part.type] = Number(part.value);
    }
  }

  const wallAsUtcMs = Date.UTC(
    values['year'],
    values['month'] - 1,
    values['day'],
    values['hour'] === 24 ? 0 : values['hour'],
    values['minute'],
    values['second'],
  );

  return wallAsUtcMs - utcMs;
}

/**
 * Converts a local calendar date (YYYY-MM-DD) and wall-clock time (HH:mm)
 * in Europe/Rome into an ISO 8601 UTC string ending with 'Z'.
 */
export function romeLocalToUtcIso(date: string, time: string): string | null {
  const dateMatch = DATE_PATTERN.exec(date.trim());
  const timeMatch = TIME_PATTERN.exec(time.trim());
  if (!dateMatch || !timeMatch) {
    return null;
  }

  const year = Number(dateMatch[1]);
  const month = Number(dateMatch[2]);
  const day = Number(dateMatch[3]);
  const hour = Number(timeMatch[1]);
  const minute = Number(timeMatch[2]);

  if (month < 1 || month > 12 || day < 1 || day > 31 || hour > 23 || minute > 59) {
    return null;
  }

  const calendarCheck = new Date(Date.UTC(year, month - 1, day));
  if (
    calendarCheck.getUTCFullYear() !== year ||
    calendarCheck.getUTCMonth() !== month - 1 ||
    calendarCheck.getUTCDate() !== day
  ) {
    return null;
  }

  const desiredWallUtcMs = Date.UTC(year, month - 1, day, hour, minute, 0, 0);
  const firstOffset = getRomeOffsetMs(desiredWallUtcMs);
  const firstCandidate = desiredWallUtcMs - firstOffset;
  const refinedOffset = getRomeOffsetMs(firstCandidate);
  const finalUtcMs = desiredWallUtcMs - refinedOffset;

  return new Date(finalUtcMs).toISOString();
}

/**
 * Formats an ISO 8601 UTC timestamp as HH:mm in Europe/Rome.
 */
export function utcIsoToRomeTime(isoString: string): string {
  const timestamp = Date.parse(isoString);
  if (Number.isNaN(timestamp)) {
    return '';
  }

  return new Intl.DateTimeFormat('it-IT', {
    timeZone: 'Europe/Rome',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).format(new Date(timestamp));
}

/**
 * Returns today's date (YYYY-MM-DD) in Europe/Rome.
 */
export function todayInRome(referenceDate: Date = new Date()): string {
  const parts = ROME_PARTS_FORMATTER.formatToParts(referenceDate);
  const values: Record<string, string> = {};
  for (const part of parts) {
    if (part.type !== 'literal') {
      values[part.type] = part.value;
    }
  }
  return `${values['year']}-${values['month']}-${values['day']}`;
}

/**
 * Checks whether a space has at least one scheduled unavailability overlapping [startIso, endIso).
 */
export function hasOverlappingUnavailability(
  space: Pick<SpaceDto, 'unavailabilities'>,
  startIso: string,
  endIso: string,
): boolean {
  const startMs = Date.parse(startIso);
  const endMs = Date.parse(endIso);
  if (Number.isNaN(startMs) || Number.isNaN(endMs) || endMs <= startMs) {
    return false;
  }

  return (space.unavailabilities ?? []).some((window) => {
    const windowStart = Date.parse(window.start);
    const windowEnd = Date.parse(window.end);
    return !Number.isNaN(windowStart) && !Number.isNaN(windowEnd) && windowStart < endMs && windowEnd > startMs;
  });
}

/**
 * Adds or subtracts calendar days from a YYYY-MM-DD date string.
 */
export function shiftIsoDate(dateString: string, deltaDays: number): string {
  const match = DATE_PATTERN.exec(dateString.trim());
  if (!match) {
    return todayInRome();
  }

  const utcDate = new Date(
    Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3]) + deltaDays),
  );
  const year = utcDate.getUTCFullYear();
  const month = String(utcDate.getUTCMonth() + 1).padStart(2, '0');
  const day = String(utcDate.getUTCDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/**
 * Builds a continuous daily timeline (08:00-23:00 Europe/Rome) showing free, partially occupied,
 * fully occupied, maintenance-unavailable, or inactive intervals for a space on a chosen day.
 */
export function buildDailyTimeline(
  space: SpaceDto,
  availability: SpaceAvailabilityDto,
  date: string,
): DailyTimelineSegment[] {
  const dayOpenIso = romeLocalToUtcIso(date, '08:00');
  const dayCloseIso = romeLocalToUtcIso(date, '23:00');
  if (!dayOpenIso || !dayCloseIso) {
    return [];
  }

  const dayOpenMs = Date.parse(dayOpenIso);
  const dayCloseMs = Date.parse(dayCloseIso);
  const capacity = availability.capacity > 0 ? availability.capacity : space.capacity;

  if (!space.isActive) {
    return [
      {
        startIso: dayOpenIso,
        endIso: dayCloseIso,
        startTimeRome: '08:00',
        endTimeRome: '23:00',
        status: 'inactive',
        occupiedSeats: capacity,
        freeSeats: 0,
        capacity,
        reason: 'Spazio disattivato e non prenotabile.',
      },
    ];
  }

  const clampedUnavailabilities = (space.unavailabilities ?? [])
    .map((u) => ({
      startMs: Math.max(dayOpenMs, Date.parse(u.start)),
      endMs: Math.min(dayCloseMs, Date.parse(u.end)),
      reason: u.reason,
    }))
    .filter((u) => !Number.isNaN(u.startMs) && !Number.isNaN(u.endMs) && u.startMs < u.endMs);

  const rawSlots = availability.slots ?? [];
  const isSyntheticFullDayBlock =
    rawSlots.length === 1 &&
    !rawSlots[0].isAvailable &&
    Date.parse(rawSlots[0].end) - Date.parse(rawSlots[0].start) >= 24 * 3_600_000 &&
    clampedUnavailabilities.length > 0;

  const clampedBookingSlots = (isSyntheticFullDayBlock ? [] : rawSlots)
    .map((s) => ({
      startMs: Math.max(dayOpenMs, Date.parse(s.start)),
      endMs: Math.min(dayCloseMs, Date.parse(s.end)),
      occupiedSeats: s.occupiedSeats,
      freeSeats: s.freeSeats,
      isAvailable: s.isAvailable,
    }))
    .filter((s) => !Number.isNaN(s.startMs) && !Number.isNaN(s.endMs) && s.startMs < s.endMs);

  const boundarySet = new Set<number>([dayOpenMs, dayCloseMs]);
  for (const u of clampedUnavailabilities) {
    boundarySet.add(u.startMs);
    boundarySet.add(u.endMs);
  }
  for (const s of clampedBookingSlots) {
    boundarySet.add(s.startMs);
    boundarySet.add(s.endMs);
  }

  const boundaries = Array.from(boundarySet).sort((a, b) => a - b);
  const rawSegments: DailyTimelineSegment[] = [];

  for (let i = 0; i < boundaries.length - 1; i++) {
    const segStart = boundaries[i];
    const segEnd = boundaries[i + 1];
    if (segStart >= segEnd) {
      continue;
    }

    const startIso = new Date(segStart).toISOString();
    const endIso = new Date(segEnd).toISOString();
    const startTimeRome = utcIsoToRomeTime(startIso);
    const endTimeRome = utcIsoToRomeTime(endIso);

    const activeUnavail = clampedUnavailabilities.find(
      (u) => u.startMs <= segStart && u.endMs >= segEnd,
    );
    if (activeUnavail) {
      rawSegments.push({
        startIso,
        endIso,
        startTimeRome,
        endTimeRome,
        status: 'unavailable',
        occupiedSeats: capacity,
        freeSeats: 0,
        capacity,
        reason: activeUnavail.reason,
      });
      continue;
    }

    const activeSlot = clampedBookingSlots.find((s) => s.startMs <= segStart && s.endMs >= segEnd);
    if (activeSlot) {
      const isDedicated = availability.accessMode === 'Dedicated';
      const freeSeats = isDedicated ? 0 : Math.max(0, activeSlot.freeSeats);
      const occupiedSeats = isDedicated
        ? capacity
        : Math.min(capacity, Math.max(0, activeSlot.occupiedSeats));
      const status: TimelineSegmentStatus =
        isDedicated || freeSeats === 0 || !activeSlot.isAvailable ? 'occupied' : 'partial';

      rawSegments.push({
        startIso,
        endIso,
        startTimeRome,
        endTimeRome,
        status,
        occupiedSeats,
        freeSeats,
        capacity,
      });
      continue;
    }

    rawSegments.push({
      startIso,
      endIso,
      startTimeRome,
      endTimeRome,
      status: 'free',
      occupiedSeats: 0,
      freeSeats: capacity,
      capacity,
    });
  }

  const merged: DailyTimelineSegment[] = [];
  for (const seg of rawSegments) {
    const prev = merged[merged.length - 1];
    if (
      prev &&
      prev.endIso === seg.startIso &&
      prev.status === seg.status &&
      prev.occupiedSeats === seg.occupiedSeats &&
      prev.freeSeats === seg.freeSeats &&
      prev.reason === seg.reason
    ) {
      prev.endIso = seg.endIso;
      prev.endTimeRome = seg.endTimeRome;
    } else {
      merged.push({ ...seg });
    }
  }

  return merged;
}
