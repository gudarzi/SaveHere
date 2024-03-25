import { useState, useEffect } from 'react';

interface FileSystemInfo {
    Name: string;
    FullName: string;
    Type: 'file' | 'directory';
    Children?: FileSystemInfo[];
    Length?: number;
    Extension?: string;
}

const DownloadedFilesList = () => {
    const [data, setData] = useState<FileSystemInfo[]>([]);

    useEffect(() => {
        fetch('/api/file/list')
            .then((res) => res.json())
            .then((result) => {
                setData(result);
            })
            .catch((err) => {
                console.error(err);
            });
    }, []);

    const renderNode = (node: FileSystemInfo, level: number) => {
        if (node.Type === 'directory') {
            return (
                <li key={node.FullName}>
                    <FileNode
                        node={node}
                        level={level}
                        renderNode={renderNode}
                    />
                </li>
            );
        } else {
            return (
                <li key={node.FullName} className="flex items-center w-full p-1 text-sm font-medium text-left text-gray-800 rounded-lg focus:outline-none dark:text-white hover:bg-gray-100 dark:hover:bg-gray-700">
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-5 h-5 m-1 dark:text-slate-300">
                        <path d="M5.625 1.5c-1.036 0-1.875.84-1.875 1.875v17.25c0 1.035.84 1.875 1.875 1.875h12.75c1.035 0 1.875-.84 1.875-1.875V12.75A3.75 3.75 0 0 0 16.5 9h-1.875a1.875 1.875 0 0 1-1.875-1.875V5.25A3.75 3.75 0 0 0 9 1.5H5.625Z" />
                        <path d="M12.971 1.816A5.23 5.23 0 0 1 14.25 5.25v1.875c0 .207.168.375.375.375H16.5a5.23 5.23 0 0 1 3.434 1.279 9.768 9.768 0 0 0-6.963-6.963Z" />
                    </svg>
                    {node.Name} - {node.Length} bytes, {node.Extension}
                </li>
            );
        }
    };

    return (
        <div className="p-3 bg-slate-100 dark:bg-gray-600 rounded-md w-4/5 mx-auto my-1">
            <h3 className="font-bold text-lg ml-2 dark:text-slate-100">
                Downloaded Files
            </h3>
            <ul className="bg-[#11111122] dark:bg-[#FFFFFF22] p-3 text-sm leading-6 rounded-md">
                {data.map((node) => renderNode(node, 1))}
            </ul>
        </div>
    );
};

const FileNode = ({
    node,
    level,
    renderNode,
}: {
    node: FileSystemInfo;
    level: number;
    renderNode: (node: FileSystemInfo, level: number) => JSX.Element;
}) => {
    const [open, setOpen] = useState(false);

    const handleClick = () => {
        setOpen(!open);
    };

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