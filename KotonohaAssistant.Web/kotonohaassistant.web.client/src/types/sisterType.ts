export const Kotonoha = {
    Akane: 0,
    Aoi: 1,
} as const;

export type Kotonoha = (typeof Kotonoha)[keyof typeof Kotonoha]