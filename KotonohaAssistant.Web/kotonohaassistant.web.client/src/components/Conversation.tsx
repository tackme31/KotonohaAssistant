import { useState, useRef, useCallback, useEffect } from 'react';
import { hasTriggerWords } from '../triggerWords';
import { useSpeechRecognition } from '../hooks/useSpeechRecognition';
import * as signalR from '@microsoft/signalr';

const useChatHubConnection = () => {
    const [connection, setConnection] = useState<signalR.HubConnection | null>(null);

    useEffect(() => {
        const newConnection = new signalR.HubConnectionBuilder()
            .withUrl("chathub") // SignalRのURL
            .build();

        newConnection.start()
            .then(() => {
                console.log("SignalR接続成功");
                setConnection(newConnection);
            })
            .catch((err) => {
                console.error("SignalR接続エラー", err);
            });

        return () => {
            newConnection.stop();
        };
    }, []);

    return connection;
}

export const Conversation = () => {
    const [messages, setMessages] = useState<string[]>([]);
    const [isInConversation, setIsInConversation] = useState<boolean>(false);
    const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const connection = useChatHubConnection();

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
        resetInactivityTimer();

        const lastResult = event.results[event.results.length - 1];
        if (!lastResult.isFinal) return;

        const lastTranscript = lastResult[0].transcript;
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
    }, [isInConversation, resetInactivityTimer, hasTriggerWords, connection]);

    useEffect(() => {
        if (connection) {
            connection.on("ReceiveMessage", (message: string) => {
                setMessages(prev => [...prev, message]);
            });
        }
    }, [connection])

    useSpeechRecognition({
        lang: "ja-JP",
        continuous: true,
        interimResults: true,
        onResult: handleSpeechResult
    })

    return (
        <div>
            <div>
                {messages.map((message, i) => (<p key={i}>{message}</p>))}
            </div>
            <p>Conversation: {isInConversation ? 'ON' : 'OFF'}</p>
        </div>
    );
}
