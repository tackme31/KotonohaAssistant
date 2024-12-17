import './App.css';
import { Conversation } from './components/Conversation';

function App() {
    return (
        <div>
            <h1 id="tabelLabel">Kotonoha Assistant</h1>
            <div>
                <Conversation />
            </div>
        </div>
    );
}

export default App;