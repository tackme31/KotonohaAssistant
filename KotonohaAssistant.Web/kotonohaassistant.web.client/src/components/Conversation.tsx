import { useState, useRef, useCallback } from 'react';
import './App.css';
import { hasTriggerWords } from '../triggerWords';
import { useSpeechRecognition } from '../hooks/useSpeechRecognition';

export const Conversation = () => {
    const [messages, setMessages] = useState<string[]>([]);
    const [isInConversation, setIsInConversation] = useState<boolean>(false);
    const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

    // 無言が5秒以上続いた場合にフラグをfalseにする
    const resetInactivityTimer = () => {
        if (timeoutRef.current) {
            clearTimeout(timeoutRef.current);
        }

        timeoutRef.current = setTimeout(() => {
            setIsInConversation(false);
        }, 5000); // 5秒間無言が続いたら反応を停止
    };

    const handleSpeechResult = useCallback((event: SpeechRecognitionEvent) => {
        resetInactivityTimer();

        const lastResult = event.results[event.results.length - 1];
        if (!lastResult.isFinal) return;

        const lastTranscript = lastResult[0].transcript;
        if (isInConversation) {
            setMessages(prev => [...prev, lastTranscript]);
        }

        if (hasTriggerWords(lastTranscript)) {
            if (!isInConversation) {
                setIsInConversation(true);
                setMessages(prev => [...prev, lastTranscript]);
            }
        }
    }, [isInConversation, resetInactivityTimer, hasTriggerWords]);

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
