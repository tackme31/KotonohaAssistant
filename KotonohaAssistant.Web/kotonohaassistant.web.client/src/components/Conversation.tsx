import { useState, useRef, useCallback, useEffect } from 'react';
import { hasTriggerWords } from '../triggerWords';
import { useSpeechRecognition } from '../hooks/useSpeechRecognition';
import { useChatHubConnection } from '../hooks/useChatHubConnection';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faMicrophone, faMicrophoneSlash } from '@fortawesome/free-solid-svg-icons'
import { ConversationResult } from '../types/conversationResult';
import { ChatBox } from './ChatBox';
import { ChatMessage } from '../types/chatMessage';


// 返事を始めるまでのタイムリミット
const limit = 7500;

export const Conversation = () => {
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const [talkingText, setTalkingText] = useState<string>();
    const [isYourTurn, setIsYourTurn] = useState<boolean>(true);
    const [isInConversation, setIsInConversation] = useState<boolean>(false);
    const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const [connectionStatus, connection] = useChatHubConnection();

    // 無言が指定時間以上続いた場合にフラグをfalseにする
    const resetInactivityTimer = useCallback(() => {
        if (timeoutRef.current) {
            clearTimeout(timeoutRef.current);
        }

        timeoutRef.current = setTimeout(() => {
            setIsInConversation(false);
        }, limit);
    }, []);

    const handleSpeechResult = useCallback(async (event: SpeechRecognitionEvent) => {
        if (!isYourTurn) {
            return;
        }

        resetInactivityTimer();

        const lastResult = event.results[event.results.length - 1];
        const lastTranscript = lastResult[0].transcript;
        setTalkingText(lastTranscript);
        if (!lastResult.isFinal) {
            return;
        }

        if (isInConversation) {
            setMessages(prev => [...prev, {
                text: lastTranscript
            }]);

            // TODO: 終了ワード「なんでもない」「さようなら」とかが含まれていると、終了する
            if (lastTranscript.includes('何でもない') || lastTranscript.includes('会話中止')) {
                setIsInConversation(false);
                return;
            }

            if (connection) {
                // 会話中のメッセージもAPIに送信
                await connection.send("SendMessage", lastTranscript);
                setTalkingText("");
            }
        }

        if (hasTriggerWords(lastTranscript)) {
            if (!isInConversation) {
                setIsInConversation(true);
                setMessages(prev => [...prev, {
                    text: lastTranscript
                }]);

                if (connection) {
                    // トリガーワード検出時にAPIにメッセージを送信
                    await connection.send("SendMessage", lastTranscript); // "SendMessage" はサーバー側のメソッド名
                    setTalkingText("");
                }
            }
        }

    }, [isInConversation, resetInactivityTimer, hasTriggerWords, connection, isYourTurn]);

    useSpeechRecognition({
        lang: "ja-JP",
        continuous: true,
        interimResults: true,
        onResult: handleSpeechResult
    })

    useEffect(() => {
        if (connection) {
            connection.on("Generated", (data: string) => {
                setIsYourTurn(false);

                const result = JSON.parse(data) as ConversationResult;
                console.log(result)
                setMessages(prev => [...prev, {
                    text: result.message,
                    talking: result.sister,
                    functions: result.functions
                }]);
            });

            connection.on("Complete", () => {
                setIsYourTurn(true);
                resetInactivityTimer();
                setIsInConversation(true);
            });
        }

        return () => {
            if (connection) {
                connection.off("Generated");
                connection.off("Complete");
            }
        }
    }, [connection, resetInactivityTimer])

    return (
        <div>
            <div style={{ display: "flex", gap: 30 }}>
                <div style={{ width: "30%" }}>
                    <h2>ステータス</h2>
                    <div>
                        <p>Is your turn: <b>{isYourTurn ? "YES" : "NO"}</b></p>
                        <p>Is in conversation: <b>{isInConversation ? "YES" : "NO"}</b></p>
                        <p>SignalR connection: <b>{connectionStatus}</b></p>
                    </div>

                    <h2>ウェイクワード</h2>
                    <div>
                        <p>「ねえ、あかねちゃん」</p>
                        <p>「ねえ、あおいちゃん」</p>
                        <p>「あかねちゃん、いる？」</p>
                        <p>「あおいちゃん、いる？」</p>
                    </div>

                    <h2>音声入力</h2>
                    <div style={{ display: "flex", alignItems: "center", gap: 5, borderBottom: "solid 1px", height: 50 }}>
                        <div style={{ width: 30, display: "flex", justifyContent: "center" }}>
                            {isYourTurn
                                ? <FontAwesomeIcon icon={faMicrophone} style={{ color: isInConversation ? "red" : undefined }} />
                                : <FontAwesomeIcon icon={faMicrophoneSlash} style={{ color: isInConversation ? "red" : undefined }} />}
                        </div>
                        {isYourTurn
                            ? <p>{talkingText}</p>
                            : <p>(ボイス再生中)</p>}
                    </div>
                </div>
                <div style={{ width: "60%" }}>
                    <h2>会話履歴</h2>
                    {messages.map((message, i) => (
                        <>
                            {message.functions != null && message.functions.map(func => (
                                <div style={{ background: "#f5f5f5", color: "#adadad", padding: "5px 20px 5px", margin: "5px 0px" }}>
                                    <details>
                                        <summary>
                                            {func.name}({Object.entries(func.arguments).map(([key, value]) => `${key}=${value}`).join(", ")})
                                        </summary>
                                        <p style={{ whiteSpace: "pre-line" }}>{func.result}</p>
                                    </details>
                                </div>
                            ))}
                            <ChatBox key={i} message={message} />
                        </>
                    ))}
                </div>
            </div>
        </div>
    );
}
