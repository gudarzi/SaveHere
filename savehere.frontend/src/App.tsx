import { useState } from 'react';
import './App.css'
import DownloadedFilesList from './components/DownloadedFilesList'

function App() {
  const [isDarkMode, setDarkMode] = useState(true);

  const toggleDarkMode = () => {
    setDarkMode(!isDarkMode);
    const newTheme = isDarkMode ? 'light' : 'dark';
    document.documentElement.setAttribute('data-theme', newTheme);
  };

  return (
    <div className={`App ${isDarkMode ? 'dark' : ''}`}>
      <button className="" onClick={toggleDarkMode}>
        {isDarkMode ? 'Light Mode' : 'Dark Mode'}
      </button>
      <h1 className="text-3xl font-bold underline">
        Hello world!
      </h1>

      <DownloadedFilesList />
    </div>
  );
}

export default App