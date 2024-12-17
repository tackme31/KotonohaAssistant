export const triggerWords = [
    'ねえあおいちゃん',
    'ねえ葵ちゃん',

    'ねあおいちゃん',
    'ね葵ちゃん',

    'あおいちゃんいる',
    '葵ちゃんいる',

    'ねえあかねちゃん',
    'ねえ茜ちゃん',

    'ねあかねちゃん',
    'ね茜ちゃん',

    '茜ちゃんいる',
    'あかねちゃんいる'
] as const;

export const hasTriggerWords = (transcript: string) => {
    const removed = transcript.replace(/[、。？?]/g, '');
    return triggerWords.some(word => removed.includes(word))
}