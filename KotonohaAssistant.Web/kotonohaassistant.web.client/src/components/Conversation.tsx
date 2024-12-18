import { useState, useRef, useCallback, useEffect } from 'react';
import { hasTriggerWords } from '../triggerWords';
import { useSpeechRecognition } from '../hooks/useSpeechRecognition';
import { useChatHubConnection } from '../hooks/useChatHubConnection';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome'
import { faMicrophone, faMicrophoneSlash } from '@fortawesome/free-solid-svg-icons'
import { ConversationResult } from '../types/ConversationResult';


// 返事を始めるまでのタイムリミット
const limit = 7500;

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
            <p>{message.replace(/^(茜|葵): /, "")}</p>
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
                setTalkingText("");
            }
        }

        if (hasTriggerWords(lastTranscript)) {
            if (!isInConversation) {
                setIsInConversation(true);
                setMessages(prev => [...prev, lastTranscript]);

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
                setMessages(prev => [...prev, result.message]);
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

                    <h2>トリガーワード</h2>
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
                                : <FontAwesomeIcon icon={faMicrophoneSlash} style={{ color: "red" }} />}
                        </div>
                        {isYourTurn
                            ? <p>{talkingText}</p>
                            : <p>(ボイス再生中)</p>}
                    </div>
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
