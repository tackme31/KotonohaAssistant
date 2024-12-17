import { useState, useRef, useCallback, useEffect } from 'react';
import { hasTriggerWords } from '../triggerWords';
import { useSpeechRecognition } from '../hooks/useSpeechRecognition';
import { useChatHubConnection } from '../hooks/useChatHubConnection';

export const Conversation = () => {
    const [messages, setMessages] = useState<string[]>([]);
    const [talkingText, setTalkingText] = useState<string>();
    const [isYourTurn, setIsYourTurn] = useState<boolean>(true);
    const [isInConversation, setIsInConversation] = useState<boolean>(false);
    const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const [connectionStatus, connection] = useChatHubConnection();

    // 無言が5秒以上続いた場合にフラグをfalseにする
    const resetInactivityTimer = useCallback(() => {
        if (timeoutRef.current) {
            clearTimeout(timeoutRef.current);
        }

        timeoutRef.current = setTimeout(() => {
            setIsInConversation(false);
        }, 5000); // 5秒間無言が続いたら反応を停止
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
            setMessages(prev => [...prev, "私: " + lastTranscript]);
            if (connection) {
                // 会話中のメッセージもAPIに送信
                await connection.send("SendMessage", lastTranscript);
            }
        }

        if (hasTriggerWords(lastTranscript)) {
            if (!isInConversation) {
                setIsInConversation(true);
                setMessages(prev => [...prev, "私: " + lastTranscript]);

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
            <div>
                <div>
                    {messages.map((message, i) => (
                        <div>
                            <p key={i}>{message}</p>
                            <hr />
                        </div>))}
                </div>
                {isYourTurn && <p>私: {talkingText}</p>}
                <p>あなたの番?: {isYourTurn ? "YES" : "NO"}</p>
                <p>会話中?: {isInConversation ? "YES" : "NO"}</p>
                <p>接続状況: {connectionStatus}</p>
            </div>
        </div>
    );
}
