import { useEffect, useRef } from 'react';

export type SpeechRecognitionOptions = {
    lang?: string;
    continuous?: boolean;
    interimResults?: boolean;
    onResult: (event: SpeechRecognitionEvent) => void;
};

export const useSpeechRecognition = ({ lang = 'ja-JP', continuous = true, interimResults = true, onResult }: SpeechRecognitionOptions) => {
    const recognitionRef = useRef<SpeechRecognition>();

    useEffect(() => {
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognition) {
            alert("このブラウザはSpeechRecognitionをサポートしていません。");
            return;
        }

        const recognition = new SpeechRecognition();
        recognition.lang = lang;
        recognition.continuous = continuous;
        recognition.interimResults = interimResults;
        recognition.onresult = onResult;

        recognitionRef.current = recognition;
        recognition.start();
        return () => recognition.stop();
    }, [lang, continuous, interimResults, onResult]);

    return recognitionRef;
};