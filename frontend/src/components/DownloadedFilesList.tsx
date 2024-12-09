import { useState, useEffect } from 'react';
import { PreviewVideoFile } from './VideoFilePreview';

interface FileSystemInfo {
    Name: string;
    FullName: string;
    Type: 'file' | 'directory';
    Children?: FileSystemInfo[];
    Length?: number;
    Extension?: string;
}

const DownloadedFilesList = (props: { dummy: string }) => {
    const [data, setData] = useState<FileSystemInfo[]>([]);

    useEffect(() => {
        fetchList()
    }, [props.dummy]);

    useEffect(() => {
        fetchList()
    }, []);

    const fetchList = async () => await fetch('/api/file/list')
        .then((res) => res.json())
        .then((result) => {
            setData(result);
        })
        .catch((err) => {
            setData([])
            console.error(err);
        })

    const convertSize = (sizeBytes: number | undefined) => {
        if (sizeBytes === undefined) return "0B";
        if (sizeBytes === 0) return "0B";
        const sizeName = ["B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];
        const i = Math.floor(Math.log(sizeBytes) / Math.log(1024));
        const p = Math.pow(1024, i);
        const s = Math.round(sizeBytes / p * 100) / 100; // Keep two decimal places
        return `${s} ${sizeName[i]}`;
    };

    const renderNode = (node: FileSystemInfo, level: number) => {
        const handleDelete = async () => {
            try {
                const response = await fetch(`api/File/delete`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: `"${node.Name}"`,
                });

                if (response.ok) {
                    fetchList();
                } else {
                    console.error('Failed to delete item:', response.statusText);
                }
            } catch (error) {
                console.error('Error:', error);
            }
        }

        if (node.Type === 'directory') {
            return (
                <li key={node.FullName}>
                    <FileNode
                        node={node}
                        level={level}
                        renderNode={renderNode}
                        fetchList={fetchList}
                    />
                </li>
            );
        } else {
            return (
                <li key={node.FullName} className="container mx-auto flex justify-between w-full p-1 text-sm font-medium text-left text-gray-800 rounded-lg focus:outline-none dark:text-white hover:bg-gray-100 dark:hover:bg-gray-700">
                    <div className="container mx-auto flex justify-left items-center">
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-5 h-5 m-1 dark:text-slate-300">
                            <path d="M5.625 1.5c-1.036 0-1.875.84-1.875 1.875v17.25c0 1.035.84 1.875 1.875 1.875h12.75c1.035 0 1.875-.84 1.875-1.875V12.75A3.75 3.75 0 0 0 16.5 9h-1.875a1.875 1.875 0 0 1-1.875-1.875V5.25A3.75 3.75 0 0 0 9 1.5H5.625Z" />
                            <path d="M12.971 1.816A5.23 5.23 0 0 1 14.25 5.25v1.875c0 .207.168.375.375.375H16.5a5.23 5.23 0 0 1 3.434 1.279 9.768 9.768 0 0 0-6.963-6.963Z" />
                        </svg>
                        <a target='_blank' href={`/files/${node.Name}`} className='dark:text-blue-800 dark:visited:text-purple-800 text-blue-500 visited:text-purple-500 mr-2'>{node.Name}</a>
                    </div>
                    <div className="ml-1 px-2 rounded-xl whitespace-nowrap content-center bg-gray-400 dark:bg-gray-700">
                        {convertSize(node.Length)}
                    </div>
                    <div className="ml-1 px-2 rounded-xl whitespace-nowrap content-center bg-gray-400 dark:bg-gray-700">
                        {node.Extension?.substring(1)}
                    </div>
                    <div className="ml-1 px-2 rounded-xl whitespace-nowrap content-center bg-gray-400 dark:bg-gray-700">
                        <button onClick={handleDelete} className="p-1 rounded-full hover:bg-[#FFFFFF33]">
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-5 h-5">
                                <path fillRule="evenodd" d="M16.5 4.478v.227a48.816 48.816 0 0 1 3.878.512.75.75 0 1 1-.256 1.478l-.209-.035-1.005 13.07a3 3 0 0 1-2.991 2.77H8.084a3 3 0 0 1-2.991-2.77L4.087 6.66l-.209.035a.75.75 0 0 1-.256-1.478A48.567 48.567 0 0 1 7.5 4.705v-.227c0-1.564 1.213-2.9 2.816-2.951a52.662 52.662 0 0 1 3.369 0c1.603.051 2.815 1.387 2.815 2.951Zm-6.136-1.452a51.196 51.196 0 0 1 3.273 0C14.39 3.05 15 3.684 15 4.478v.113a49.488 49.488 0 0 0-6 0v-.113c0-.794.609-1.428 1.364-1.452Zm-.355 5.945a.75.75 0 1 0-1.5.058l.347 9a.75.75 0 1 0 1.499-.058l-.346-9Zm5.48.058a.75.75 0 1 0-1.498-.058l-.347 9a.75.75 0 0 0 1.5.058l.345-9Z" clipRule="evenodd" />
                            </svg>
                        </button>
                    </div>
                    <PreviewVideoFile videoUrl={window.location.protocol + '//' + window.location.host + '/files/' + node.Name} />

                </li>
            );
        }
    };

    return (
        <div className="p-3 bg-slate-100 dark:bg-gray-600 rounded-md w-4/5 mx-auto my-1">
            <div className='flex flex-row'>
                <h3 className="font-bold text-lg ml-2 dark:text-slate-100">
                    Downloaded Files
                </h3>
                <button className="ml-2" onClick={fetchList}>
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-6 h-6 p-1 rounded-full hover:bg-[#FFFFFF33] dark:text-slate-100">
                        <path fillRule="evenodd" d="M4.755 10.059a7.5 7.5 0 0 1 12.548-3.364l1.903 1.903h-3.183a.75.75 0 1 0 0 1.5h4.992a.75.75 0 0 0 .75-.75V4.356a.75.75 0 0 0-1.5 0v3.18l-1.9-1.9A9 9 0 0 0 3.306 9.67a.75.75 0 1 0 1.45.388Zm15.408 3.352a.75.75 0 0 0-.919.53 7.5 7.5 0 0 1-12.548 3.364l-1.902-1.903h3.183a.75.75 0 0 0 0-1.5H2.984a.75.75 0 0 0-.75.75v4.992a.75.75 0 0 0 1.5 0v-3.18l1.9 1.9a9 9 0 0 0 15.059-4.035.75.75 0 0 0-.53-.918Z" clipRule="evenodd" />
                    </svg>
                </button>
            </div>
            <ul className="bg-[#11111122] dark:bg-[#FFFFFF22] p-3 text-sm leading-6 rounded-md">
                {data && data.length > 0 ? (
                    data.map((node) => renderNode(node, 1))
                ) : (
                    <li className="text-center font-bold">Nothing to Show!</li>
                )}
            </ul>
        </div>
    );
};

const FileNode = ({
    node,
    level,
    renderNode,
    fetchList,
}: {
    node: FileSystemInfo
    level: number
    renderNode: (node: FileSystemInfo, level: number) => JSX.Element
    fetchList: () => void
}) => {
    const [open, setOpen] = useState(false);

    const handleClick = () => {
        setOpen(!open);
    }

    const handleDelete = async () => {
        try {
            const response = await fetch(`api/File/delete`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: `"${node.Name}"`,
            });

            if (response.ok) {
                fetchList();
            } else {
                console.error('Failed to delete item:', response.statusText);
            }
        } catch (error) {
            console.error('Error:', error);
        }
    }

    return (
        <>
            <button
                className="flex items-center w-full p-1 text-sm font-medium text-left text-gray-800 rounded-lg focus:outline-none dark:text-white hover:bg-gray-100 dark:hover:bg-gray-700"
                onClick={handleClick}
            >
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-5 h-5 m-1 dark:text-slate-300">
                    <path d="M19.5 21a3 3 0 0 0 3-3v-4.5a3 3 0 0 0-3-3h-15a3 3 0 0 0-3 3V18a3 3 0 0 0 3 3h15ZM1.5 10.146V6a3 3 0 0 1 3-3h5.379a2.25 2.25 0 0 1 1.59.659l2.122 2.121c.14.141.331.22.53.22H19.5a3 3 0 0 1 3 3v1.146A4.483 4.483 0 0 0 19.5 9h-15a4.483 4.483 0 0 0-3 1.146Z" />
                </svg>
                {node.Name}
                <button onClick={handleDelete} className="ml-2 p-1 rounded-full hover:bg-[#FFFFFF33]">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-5 h-5">
                        <path fillRule="evenodd" d="M16.5 4.478v.227a48.816 48.816 0 0 1 3.878.512.75.75 0 1 1-.256 1.478l-.209-.035-1.005 13.07a3 3 0 0 1-2.991 2.77H8.084a3 3 0 0 1-2.991-2.77L4.087 6.66l-.209.035a.75.75 0 0 1-.256-1.478A48.567 48.567 0 0 1 7.5 4.705v-.227c0-1.564 1.213-2.9 2.816-2.951a52.662 52.662 0 0 1 3.369 0c1.603.051 2.815 1.387 2.815 2.951Zm-6.136-1.452a51.196 51.196 0 0 1 3.273 0C14.39 3.05 15 3.684 15 4.478v.113a49.488 49.488 0 0 0-6 0v-.113c0-.794.609-1.428 1.364-1.452Zm-.355 5.945a.75.75 0 1 0-1.5.058l.347 9a.75.75 0 1 0 1.499-.058l-.346-9Zm5.48.058a.75.75 0 1 0-1.498-.058l-.347 9a.75.75 0 0 0 1.5.058l.345-9Z" clipRule="evenodd" />
                    </svg>
                </button>
            </button>
            {open && (
                <ul className="ml-4 mb-2">
                    {node.Children?.map((child) =>
                        renderNode(child, level + 1)
                    )}
                </ul>
            )}
        </>
    );
};

export default DownloadedFilesList;