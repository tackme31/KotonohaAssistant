import { Kotonoha } from "./sisterType";

export interface ConversationFunction {
    name: string;
    arguments: {
        [key: string]: unknown
    };
    result: string;
}

export interface ConversationResult {
    message: string;
    sister: Kotonoha;
    functions?: ConversationFunction[]
}