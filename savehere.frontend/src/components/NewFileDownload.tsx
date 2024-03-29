import { useState } from 'react';

const NewFileDownload = () => {
  const [inputUrl, setInputUrl] = useState('');

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    try {
      const response = await fetch('api/FileDownloadQueueItems', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ inputUrl }),
      });

      if (!response.ok) {
        //   throw new Error('Network response was not ok');
      }

      setInputUrl(''); // Clear the input field
      // console.log('Request sent successfully');
    } catch (error) {
      console.error('Error:', error);
    }
  };

  return (
    <div className="p-3 bg-slate-100 dark:bg-gray-600 rounded-md w-4/5 mx-auto my-1 flex justify-center">
      <h3 className="font-bold text-lg ml-2 dark:text-slate-100 flex-shrink-0">
        Add New Link
      </h3>
      <form onSubmit={handleSubmit} className="flex flex-grow flex-shrink-0 min-w-0 items-center">
        <input
          type="text"
          placeholder="Enter the link to a file ..."
          className="appearance-none bg-transparent border-b-2 dark:text-white border-gray-400 mx-2 py-1 leading-tight focus:outline-none focus:border-gray-600 flex-grow flex-shrink min-w-0"
          value={inputUrl}
          onChange={(event) => setInputUrl(event.target.value)}
        />
        <button type="submit" className="bg-blue-500 hover:bg-blue-700 text-white font-bold py-1 px-2 rounded focus:outline-none focus:shadow-outline">
          Add
        </button>
      </form>
    </div>
  );
};

export default NewFileDownload;