import { useState, useRef, useCallback, useEffect } from 'react';
import { hasTriggerWords } from '../triggerWords';
import { useSpeechRecognition } from '../hooks/useSpeechRecognition';
import { useChatHubConnection } from '../hooks/useChatHubConnection';

// 返事を始めるまでのタイムリミット
const limit = 10 * 1000;

const ChatBox = ({ message }: { message: string }) => {
    let talking = "me"
    if (message.startsWith("茜:")) {
        talking = "akane"
    }
    if (message.startsWith("葵:")) {
        talking = "aoi"
    }

    return (<div style={{
        display: "flex",
        justifyContent: talking == "me" ? "flex-end" : "flex-start"
    }}>
        <div style={{
            margin: "7px 10px",
            padding: "0px 20px",
            borderRadius: 10,
            color: "black",
            backgroundColor: {
                "akane": "pink",
                "aoi": "lightblue",
                "me": "lightgrey"
            }[talking]
        }}>
            <p style={{lineHeight: 1} }>{message.replace(/^(茜|葵): /, "")}</p>
        </div>
    </div>);
}

export const Conversation = () => {
    const [messages, setMessages] = useState<string[]>([]);
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
            setMessages(prev => [...prev, lastTranscript]);
            if (connection) {
                // 会話中のメッセージもAPIに送信
                await connection.send("SendMessage", lastTranscript);
            }
        }

        if (hasTriggerWords(lastTranscript)) {
            if (!isInConversation) {
                setIsInConversation(true);
                setMessages(prev => [...prev, lastTranscript]);

                if (connection) {
                    // トリガーワード検出時にAPIにメッセージを送信
                    await connection.send("SendMessage", lastTranscript); // "SendMessage" はサーバー側のメソッド名
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
            connection.on("Generated", (message: string) => {
                setIsYourTurn(false);
                setMessages(prev => [...prev, message]);
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
            <div style={{ display: "flex" }}>
                <div style={{ width: "30%" }}>
                    <h2>ステータス</h2>
                    <div>
                        <p>あなたの番?: {isYourTurn ? "YES" : "NO"}</p>
                        <p>会話中?: {isInConversation ? "YES" : "NO"}</p>
                        <p>接続状況: {connectionStatus}</p>
                    </div>
                    <h2>音声入力</h2>
                    <p>&gt; {talkingText}</p>
                </div>
                <div style={{ width: "50%" }}>
                    <h2>会話履歴</h2>
                    {messages.map((message, i) => (
                        <ChatBox key={i} message={message} />
                    ))}
                </div>
            </div>
        </div>
    );
}
