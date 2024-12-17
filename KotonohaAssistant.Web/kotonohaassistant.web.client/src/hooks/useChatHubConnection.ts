import * as signalR from "@microsoft/signalr";
import { useEffect, useState } from "react";

type ConnectionStatus = "connecting" | "success" | "failure";

export const useChatHubConnection = (): [ConnectionStatus, signalR.HubConnection | null] => {
    const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
    const [status, setStatus] = useState<ConnectionStatus>("connecting");

    const maxRetries = 3;

    useEffect(() => {
        const tryConnect = async () => {
            let attempt = 0;

            // リトライ処理
            while (attempt < maxRetries) {
                const newConnection = new signalR.HubConnectionBuilder()
                    .withUrl("chathub")
                    .build();

                try {
                    await newConnection.start();

                    setStatus("success")
                    setConnection(newConnection);

                    return;
                } catch (err) {
                    attempt++;
                    if (attempt >= maxRetries) {
                        setStatus("failure")
                        break;
                    }

                    await new Promise((resolve) => setTimeout(resolve, 2000));
                }
            }
        };

        // 接続試行を開始
        tryConnect();

        return () => {
            if (connection) {
                connection.stop();
            }
        };
    }, []);

    return [status, connection];
}