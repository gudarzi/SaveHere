import { useState, useEffect } from 'react';

interface QueueItem {
    id: number;
    inputUrl: string;
    status: number;
}

const QueueItemsList = () => {
    const [data, setData] = useState<QueueItem[]>([]);

    useEffect(() => {
        fetchList()
    }, []);

    const fetchList = async () => await fetch('/api/FileDownloadQueueItems')
        .then((res) => res.json())
        .then((result) => {
            setData(result);
        })
        .catch((err) => {
            setData([])
            console.error(err);
        })

    const renderNode = (node: QueueItem) => {
        return (
            <li key={node.id} className="flex items-center w-full p-1 text-sm font-medium text-left text-gray-800 rounded-lg focus:outline-none dark:text-white hover:bg-gray-100 dark:hover:bg-gray-700">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-5 h-5 m-1 dark:text-slate-300">
                    <path d="M5.625 1.5c-1.036 0-1.875.84-1.875 1.875v17.25c0 1.035.84 1.875 1.875 1.875h12.75c1.035 0 1.875-.84 1.875-1.875V12.75A3.75 3.75 0 0 0 16.5 9h-1.875a1.875 1.875 0 0 1-1.875-1.875V5.25A3.75 3.75 0 0 0 9 1.5H5.625Z" />
                    <path d="M12.971 1.816A5.23 5.23 0 0 1 14.25 5.25v1.875c0 .207.168.375.375.375H16.5a5.23 5.23 0 0 1 3.434 1.279 9.768 9.768 0 0 0-6.963-6.963Z" />
                </svg>
                {node.id} - {node.inputUrl}, {node.status}
            </li>
        );
    };

    return (
        <div className="p-3 bg-slate-100 dark:bg-gray-600 rounded-md w-4/5 mx-auto my-1">
            <div className='flex flex-row'>
                <h3 className="font-bold text-lg ml-2 dark:text-slate-100">
                    Download Queue
                </h3>
                <button className="ml-2" onClick={fetchList}>
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-6 h-6 p-1 rounded-full hover:bg-[#FFFFFF33] dark:text-slate-100">
                        <path fillRule="evenodd" d="M4.755 10.059a7.5 7.5 0 0 1 12.548-3.364l1.903 1.903h-3.183a.75.75 0 1 0 0 1.5h4.992a.75.75 0 0 0 .75-.75V4.356a.75.75 0 0 0-1.5 0v3.18l-1.9-1.9A9 9 0 0 0 3.306 9.67a.75.75 0 1 0 1.45.388Zm15.408 3.352a.75.75 0 0 0-.919.53 7.5 7.5 0 0 1-12.548 3.364l-1.902-1.903h3.183a.75.75 0 0 0 0-1.5H2.984a.75.75 0 0 0-.75.75v4.992a.75.75 0 0 0 1.5 0v-3.18l1.9 1.9a9 9 0 0 0 15.059-4.035.75.75 0 0 0-.53-.918Z" clipRule="evenodd" />
                    </svg>
                </button>
            </div>
            <ul className="bg-[#11111122] dark:bg-[#FFFFFF22] p-3 text-sm leading-6 rounded-md">
                {data && data.length > 0 ? (
                    data.map((node) => renderNode(node))
                ) : (
                    <li className="text-center font-bold">Nothing to Show!</li>
                )}
            </ul>
        </div>
    );
};

export default QueueItemsList;