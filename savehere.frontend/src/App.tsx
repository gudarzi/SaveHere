import { useState } from 'react';
import './App.css'
import DownloadedFilesList from './components/DownloadedFilesList'
import QueueItemsList from './components/QueueItemsList';
import NewFileDownload from './components/NewFileDownload';

function App() {
  const [isDarkMode, setDarkMode] = useState(true);
  const [dummy1, setDummy1] = useState("")
  const [dummy2, setDummy2] = useState("")

  const toggleDarkMode = () => {
    setDarkMode(!isDarkMode);
  };

  return (
    <div className={`App ${isDarkMode ? 'dark' : ''}`}>
      <nav className="text-white p-4">
        <div className="container mx-auto flex justify-between items-center">

          <div className="container mx-auto flex justify-left items-center">
            <img src="/icon.svg" alt="Icon" />
            <svg width="200" height="50" xmlns="http://www.w3.org/2000/svg">
              <defs>
                <linearGradient id="textGradient" x1="0%" y1="25%" x2="100%" y2="75%">
                  <stop offset="0%" stopColor="red" />
                  <stop offset="100%" stopColor="#5555FFFF" />
                </linearGradient>
              </defs>
              <text x="10" y="35" fontFamily="Arial" fontSize="30" fill="url(#textGradient)">SaveHere</text>
              <rect x="10" y="38" width="140" height="2" fill="url(#textGradient)" />
            </svg>

            <a href='/filemanager' className='m-1 ml-2 underline'>File Manager</a>
            <a href='/swagger/index.html' className='m-1 ml-2 underline'>Swagger</a>
          </div>


          <button className="p-1 rounded-full hover:bg-[#FFFFFF33]" onClick={toggleDarkMode}>
            {isDarkMode ?
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="white" className="w-6 h-6">
                <path fillRule="evenodd" d="M9.528 1.718a.75.75 0 0 1 .162.819A8.97 8.97 0 0 0 9 6a9 9 0 0 0 9 9 8.97 8.97 0 0 0 3.463-.69.75.75 0 0 1 .981.98 10.503 10.503 0 0 1-9.694 6.46c-5.799 0-10.5-4.7-10.5-10.5 0-4.368 2.667-8.112 6.46-9.694a.75.75 0 0 1 .818.162Z" clipRule="evenodd" />
              </svg>
              :
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="white" className="w-6 h-6">
                <path d="M12 2.25a.75.75 0 0 1 .75.75v2.25a.75.75 0 0 1-1.5 0V3a.75.75 0 0 1 .75-.75ZM7.5 12a4.5 4.5 0 1 1 9 0 4.5 4.5 0 0 1-9 0ZM18.894 6.166a.75.75 0 0 0-1.06-1.06l-1.591 1.59a.75.75 0 1 0 1.06 1.061l1.591-1.59ZM21.75 12a.75.75 0 0 1-.75.75h-2.25a.75.75 0 0 1 0-1.5H21a.75.75 0 0 1 .75.75ZM17.834 18.894a.75.75 0 0 0 1.06-1.06l-1.59-1.591a.75.75 0 1 0-1.061 1.06l1.59 1.591ZM12 18a.75.75 0 0 1 .75.75V21a.75.75 0 0 1-1.5 0v-2.25A.75.75 0 0 1 12 18ZM7.758 17.303a.75.75 0 0 0-1.061-1.06l-1.591 1.59a.75.75 0 0 0 1.06 1.061l1.591-1.59ZM6 12a.75.75 0 0 1-.75.75H3a.75.75 0 0 1 0-1.5h2.25A.75.75 0 0 1 6 12ZM6.697 7.757a.75.75 0 0 0 1.06-1.06l-1.59-1.591a.75.75 0 0 0-1.061 1.06l1.59 1.591Z" />
              </svg>
            }
          </button>
        </div>
      </nav>

      <NewFileDownload onNewFileAdded={() => setDummy1(dummy1 + 1)} />

      <QueueItemsList dummy={dummy1} onDownloadFinished={() => setDummy2(dummy2 + 1)} />

      <DownloadedFilesList dummy={dummy2} />

    </div>
  );
}

export default App