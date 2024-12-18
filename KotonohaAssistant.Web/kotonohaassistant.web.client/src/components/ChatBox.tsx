import { ChatMessage } from "../types/chatMessage";
import { Kotonoha } from "../types/sisterType";

export const ChatBox = ({ message }: { message: ChatMessage }) => {
    let color: React.CSSProperties["color"];
    switch (message.talking) {
        case Kotonoha.Akane:
            color = "pink";
            break;
        case Kotonoha.Aoi:
            color = "lightblue";
            break
        default:
            color = "lightgray"
    }

    return (<div style={{
        display: "flex",
        justifyContent: message.talking == null ? "flex-end" : "flex-start"
    }}>
        <div style={{
            margin: "7px 0",
            padding: "0px 20px",
            borderRadius: 10,
            color: "black",
            backgroundColor: color
        }}>
            <p>{message.text}</p>
        </div>
    </div>);
}