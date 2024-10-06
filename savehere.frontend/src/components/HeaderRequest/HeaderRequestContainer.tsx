import React from "react";
import {List} from "./List";

export interface IHeaderRequestContainer {
  headerList: Array<{key: string; value: string}>;
  setHeaderList: (arg: any) => void;
  // setHeaderList: React.Dispatch<SetStateAction<[{ key: string; value: string; }]>>;
}

const HeaderRequestContainer = ({
  headerList,
  setHeaderList,
}: IHeaderRequestContainer) => {
  return (
    <div className="p-3 bg-slate-100 dark:bg-gray-600 rounded-md w-4/5 mx-auto my-1">
      <div className="flex flex-row justify-between">
        <h3 className="font-bold text-lg ml-2 dark:text-slate-100">
          Add Request Headers
        </h3>
        <button
          type="button"
          className="bg-blue-500 hover:bg-blue-700 text-white font-bold py-1 px-2 rounded focus:outline-none focus:shadow-outline"
          onClick={() => setHeaderList([...headerList, {key: "", value: ""}])}
        >
          Add
        </button>
      </div>
      <div>
        <List headerList={headerList} setHeaderList={setHeaderList} />
      </div>
    </div>
  );
};

export default HeaderRequestContainer;
