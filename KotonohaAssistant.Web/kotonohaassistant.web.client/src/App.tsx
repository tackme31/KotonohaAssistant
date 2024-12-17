import './App.css';
import * as signalR from "@microsoft/signalr";

function App() {
    const handleClick = () => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("https://localhost:5173/chathub")
            .build();

        connection.start()
            .then(() => {
                console.log("SignalR接続成功");

                // サーバーへメッセージ送信
                connection.invoke("SendMessage", "ユーザーからのメッセージ");

                // サーバーから"Hello, world"を受信
                connection.on("ReceiveMessage", (message) => {
                    console.log("受信メッセージ:", message);
                });

                // サーバーから5秒後に通知
                connection.on("ReadingComplete", (message) => {
                    console.log("読み上げ完了通知:", message);
                });
            })
            .catch(err => console.error("SignalR接続エラー:", err));
    }

    return (
        <div>
            <h1 id="tabelLabel">Kotonoha Assistant</h1>
            <div>
            </div>
            <button onClick={handleClick}>CLICK</button>
        </div>
    );

    async function populateWeatherData() {
        const response = await fetch('weatherforecast');
        const data = await response.json();
    }
}

export default App;