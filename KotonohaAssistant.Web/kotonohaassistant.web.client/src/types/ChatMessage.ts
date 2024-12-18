import { ConversationResult } from "./conversationResult";
import { Kotonoha } from "./sisterType";

export interface ChatMessage {
    text: string;
    talking?: Kotonoha;
    functions?: ConversationResult["functions"]
}